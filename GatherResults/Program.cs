﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

namespace GatherResults
{
	class Program
	{
		private static string BenchPath = "../../../app";
		private static string JavaPath = Environment.GetEnvironmentVariable("JAVA_HOME");
		private static string RunOnly = null;

		static void Main(string[] args)
		{
			if (args.Length == 2 && args[0] == "import" && File.Exists(args[1]))
			{
				File.Copy("template.xlsx", "results.xlsx", true);
				var vms = JsonConvert.DeserializeObject<ViewModel[]>(File.ReadAllText(args[1]));
				using (var doc = NGS.Templater.Configuration.Factory.Open("results.xlsx"))
					doc.Process(vms);
				Process.Start("results.xlsx");
				return;
			}
			if (args.Length > 0) BenchPath = args[0];
			if (args.Length == 3) RunOnly = args[2];
			bool exeExists = File.Exists(Path.Combine(BenchPath, "JsonBenchmark.exe"));
			bool jarExists = File.Exists(Path.Combine(BenchPath, "json-benchmark.jar"));
			if (!exeExists && !jarExists)
			{
				if (args.Length > 0 || !File.Exists("JsonBenchmark.exe"))
				{
					Console.WriteLine("Unable to find benchmark exe file: JsonBenchmark.exe in" + BenchPath);
					return;
				}
				if (args.Length > 0 || !File.Exists("json-benchmark.jar"))
				{
					Console.WriteLine("Unable to find benchmark jar file: json-benchmark.jar in" + BenchPath);
					return;
				}
				BenchPath = ".";
			}
			var java = JavaPath != null ? Path.Combine(JavaPath, "bin", "java") : "java";
			var process =
				Process.Start(
					new ProcessStartInfo
					{
						FileName = java,
						Arguments = "-version",
						RedirectStandardOutput = true,
						UseShellExecute = false
					});
			var javaVersion = process.StandardOutput.ReadToEnd();
			Console.WriteLine(javaVersion);
			Console.WriteLine(".NET: " + Environment.Version);
			int repeat = args.Length > 1 ? int.Parse(args[1]) : 2;
			RunSinglePass("Warmup .NET", true, "RevenjJsonMinimal", "Small", null, 1);
			RunSinglePass("Warmup JVM", false, "DslJsonMinimal", "Small", null, 1);
			var small1 = RunSmall(repeat, 1);
			var std1 = RunStandard(repeat, 1);
			var small100k = RunSmall(repeat, 100000);
			var small1m = RunSmall(repeat, 1000000);
			var small10m = RunSmall(repeat, 10000000);
			RunSinglePass("Warmup .NET", true, "RevenjJsonMinimal", "Standard", null, 1);
			RunSinglePass("Warmup JVM", false, "DslJsonMinimal", "Standard", null, 1);
			var std1k = RunStandard(repeat, 1000);
			var std10k = RunStandard(repeat, 10000);
			var std100k = RunStandard(repeat, 100000);
			RunSinglePass("Warmup .NET", true, "RevenjJsonMinimal", "Large", null, 1);
			RunSinglePass("Warmup JVM", false, "DslJsonMinimal", "Large", null, 1);
			var large50 = RunLarge(repeat, 50);
			var large500 = RunLarge(repeat, 500);
			var outputExcel = RunOnly == null ? "results.xlsx" : RunOnly + ".xlsx";
			File.Copy("template.xlsx", outputExcel, true);
			var vm = new ViewModel[]
			{
				ViewModel.Create("Startup times: SmallObject.Message",small1, t => t.Message),
				ViewModel.Create("Startup times: StandardObjects.Post", std1, t => t.Post),
				ViewModel.Create("100.000 SmallObjects.Message", small100k, t => t.Message),
				ViewModel.Create("1.000.000 SmallObjects.Message", small1m, t => t.Message),
				ViewModel.Create("10.000.000 SmallObjects.Message", small10m, t => t.Message),
				ViewModel.Create("100.000 SmallObjects.Complex", small100k, t => t.Complex),
				ViewModel.Create("1.000.000 SmallObjects.Complex", small1m, t => t.Complex),
				ViewModel.Create("10.000.000 SmallObjects.Complex", small10m, t => t.Complex),
				ViewModel.Create("100.000 SmallObjects.Post", small100k, t => t.Post),
				ViewModel.Create("1.000.000 SmallObjects.Post", small1m, t => t.Post),
				ViewModel.Create("10.000.000 SmallObjects.Post", small10m, t => t.Post),
				ViewModel.Create("1.000 StandardObjects.DeletePost", std1k, t => t.DeletePost),
				ViewModel.Create("10.000 StandardObjects.DeletePost", std10k, t => t.DeletePost),
				ViewModel.Create("100.000 StandardObjects.DeletePost", std100k, t => t.DeletePost),
				ViewModel.Create("1.000 StandardObjects.Post", std1k, t => t.Post),
				ViewModel.Create("10.000 StandardObjects.Post", std10k, t => t.Post),
				ViewModel.Create("100.000 StandardObjects.Post", std100k, t => t.Post),
				new ViewModel("50 LargeObjects.Book", large50),
				new ViewModel("500 LargeObjects.Book", large500),
			};
			var json = JsonConvert.SerializeObject(vm);
			File.WriteAllText(RunOnly == null ? "results.json" : RunOnly + ".json", json);
			using (var doc = NGS.Templater.Configuration.Factory.Open(outputExcel))
				doc.Process(vm);
			Process.Start(outputExcel);
		}

		class SmallTest
		{
			public List<Result> Message = new List<Result>();
			public List<Result> Complex = new List<Result>();
			public List<Result> Post = new List<Result>();
		}

		static Run<SmallTest> RunSmall(int times, int loops)
		{
			return new Run<SmallTest>
			{
				Instance = RunSmall(null, times, loops),
				Serialization = RunSmall(false, times, loops),
				Both = RunSmall(true, times, loops)
			};
		}

		static SmallTest RunSmall(bool? both, int times, int loops)
		{
			Console.Write("Gathering small (" + loops + ") ");
			Console.Write(both == null ? "instance only" : both == true ? "serialization and deserialization" : "serialization only");
			var result = new SmallTest();
			for (int i = 0; i < times; i++)
			{
				var d = GetherDuration("Small", both, loops);
				Console.Write("...");
				Console.Write(i + 1);
				result.Message.Add(d.Extract(0));
				result.Complex.Add(d.Extract(1));
				result.Post.Add(d.Extract(2));
			}
			Console.WriteLine(" ... done");
			return result;
		}

		class StandardTest
		{
			public List<Result> DeletePost = new List<Result>();
			public List<Result> Post = new List<Result>();
		}

		static Run<StandardTest> RunStandard(int times, int loops)
		{
			return new Run<StandardTest>
			{
				Instance = RunStandard(null, times, loops),
				Serialization = RunStandard(false, times, loops),
				Both = RunStandard(true, times, loops)
			};
		}

		static StandardTest RunStandard(bool? both, int times, int loops)
		{
			Console.Write("Gathering standard (" + loops + ")");
			Console.Write(both == null ? "instance only" : both == true ? "serialization and deserialization" : "serialization only");
			var result = new StandardTest();
			for (int i = 0; i < times; i++)
			{
				var d = GetherDuration("Standard", both, loops);
				Console.Write("...");
				Console.Write(i + 1);
				result.DeletePost.Add(d.Extract(0));
				result.Post.Add(d.Extract(1));
			}
			Console.WriteLine(" ... done");
			return result;
		}

		static Run<List<Result>> RunLarge(int times, int loops)
		{
			return new Run<List<Result>>
			{
				Instance = RunLarge(null, times, loops),
				Serialization = RunLarge(false, times, loops),
				Both = RunLarge(true, times, loops)
			};
		}

		static List<Result> RunLarge(bool? both, int times, int loops)
		{
			Console.Write("Gathering large (" + loops + ")");
			Console.Write(both == null ? "instance only" : both == true ? "serialization and deserialization" : "serialization only");
			var result = new List<Result>();
			for (int i = 0; i < times; i++)
			{
				result.Add(GetherDuration("Large", both, loops).Extract(0));
				Console.Write("...");
				Console.Write(i + 1);
			}
			Console.WriteLine(" ... done");
			return result;
		}

		private static readonly List<Stats> EmptyStats = new List<Stats>(new[]{
			new Stats{ Duration = null, Size = null},
			new Stats{ Duration = null, Size = null},
			new Stats{ Duration = null, Size = null}
		});

		static AggregatePass GetherDuration(string type, bool? both, int count)
		{
			var NJ = type != "Large"
				? RunSinglePass("NewtonsoftJson", true, "NewtonsoftJson", type, both, count)
				: RunSinglePass("NewtonsoftJson", true, "RevenjNewtonsoftJson", type, both, count);
			var REV = RunSinglePass("Revenj", true, "RevenjJsonMinimal", type, both, count);
			//TODO hacks since most libraries fail
			var SS = type != "Large" ? RunSinglePass("Service Stack", true, "ServiceStack", type, both, count) : EmptyStats;
			var JIL = type != "Large" ? RunSinglePass("Jil", true, "Jil", type, both, count) : EmptyStats;
			var NN = type != "Large" ? RunSinglePass("NetJSON", true, "NetJSON", type, both, count) : EmptyStats;
			var PB = RunSinglePass("ProtoBuf", true, "ProtoBuf", type, both, count);
			var JJ = RunSinglePass("Jackson", false, "JacksonAfterburner", type, both, count);
			var JD = RunSinglePass("DSL-JSON", false, "DslJsonMinimal", type, both, count);
			var KR = RunSinglePass("Kryo", false, "Kryo", type, both, count);
			var JG = type != "Large" ? RunSinglePass("Gson", false, "Gson", type, both, count) : EmptyStats;
			var JB = type == "Small" ? RunSinglePass("Boon", false, "Boon", type, both, count) : EmptyStats;
			var JA = type != "Large" ? RunSinglePass("Alibaba", false, "Alibaba", type, both, count) : EmptyStats;
			return new AggregatePass
			{
				Newtonsoft = NJ,
				Revenj = REV,
				ServiceStack = SS,
				Jil = JIL,
				NetJSON = NN,
				Protobuf = PB,
				Jackson = JJ,
				DslJson = JD,
				Kryo = KR,
				Gson = JG,
				Alibaba = JA,
				Boon = JB
			};
		}

		static List<Stats> RunSinglePass(string description, bool exe, string serializer, string type, bool? both, int count)
		{
			var result = new List<Stats>();
			if (RunOnly != null && RunOnly != serializer)
			{
				result.Add(new Stats { Duration = null, Size = null });
				result.Add(new Stats { Duration = null, Size = null });
				result.Add(new Stats { Duration = null, Size = null });
				return result;
			}
			var processName = exe ? Path.Combine(BenchPath, "JsonBenchmark.exe") : Path.Combine(JavaPath ?? ".", "bin", "java");
			var jarArg = exe ? string.Empty : "-jar \"" + Path.Combine(BenchPath, "json-benchmark.jar") + "\" ";
			var what = both == null ? " None " : both == true ? " Both " : " Serialization ";
			var info = new ProcessStartInfo(processName, jarArg + serializer + " " + type + what + count)
			{
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			var process = Process.Start(info);
			process.WaitForExit(10000 + count * 1000);
			if (both != null) Thread.Sleep(200);//let's wait for cleanup if required
			if (!process.HasExited)
			{
				Console.WriteLine();
				process.Close();
				try { process.Kill(); }
				catch { }
				Console.WriteLine("Timeout");
				result.Add(new Stats { Duration = null, Size = null });
				result.Add(new Stats { Duration = null, Size = null });
				result.Add(new Stats { Duration = null, Size = null });
				return result;
			}
			var lines = process.StandardOutput.ReadToEnd().Split('\n');
			Console.WriteLine();
			for (int i = 0; i < lines.Length / 3; i++)
			{
				var duration = lines[i * 3].Split('=');
				var size = lines[i * 3 + 1].Split('=');
				var errors = lines[i * 3 + 2].Split('=');
				try
				{
					Console.WriteLine(description + ": duration = " + duration[1].Trim() + ", size = " + size[1].Trim() + ", errors = " + errors[1].Trim());
					if (duration[1].Trim() == "-1")
						result.Add(new Stats { Duration = null, Size = null });
					else
						result.Add(new Stats { Duration = int.Parse(duration[1]), Size = long.Parse(size[1]) });
				}
				catch
				{
					result.Add(new Stats { Duration = null, Size = null });
				}
			}
			result.Add(new Stats { Duration = null, Size = null });
			result.Add(new Stats { Duration = null, Size = null });
			result.Add(new Stats { Duration = null, Size = null });
			return result;
		}
	}

	struct Stats
	{
		public int? Duration;
		public long? Size;
	}

	class AggregatePass
	{
		public List<Stats> Newtonsoft;
		public List<Stats> Revenj;
		public List<Stats> Jil;
		public List<Stats> NetJSON;
		public List<Stats> Protobuf;
		public List<Stats> ServiceStack;
		public List<Stats> Jackson;
		public List<Stats> DslJson;
		public List<Stats> Kryo;
		public List<Stats> Gson;
		public List<Stats> Alibaba;
		public List<Stats> Boon;

		public Result Extract(int index)
		{
			return new Result
			{
				Newtonsoft = Newtonsoft[index],
				Revenj = Revenj[index],
				ServiceStack = ServiceStack[index],
				Jil = Jil[index],
				Protobuf = Protobuf[index],
				NetJSON = NetJSON[index],
				Jackson = Jackson[index],
				DslJson = DslJson[index],
				Kryo = Kryo[index],
				Gson = Gson[index],
				Alibaba = Alibaba[index],
				Boon = Boon[index],
			};
		}
	}

	class Result
	{
		public Stats Newtonsoft;
		public Stats Revenj;
		public Stats Jil;
		public Stats Protobuf;
		public Stats ServiceStack;
		public Stats NetJSON;
		public Stats Jackson;
		public Stats DslJson;
		public Stats Kryo;
		public Stats Gson;
		public Stats Alibaba;
		public Stats Boon;
	}

	class Run<T>
	{
		public T Instance;
		public T Serialization;
		public T Both;
	}

	class ViewModel
	{
		public string description;
		public List<Result> instance;
		public List<Result> serialization;
		public List<Result> both;
		public ViewModel() { }
		public ViewModel(string description, Run<List<Result>> run)
			: this(description, run.Instance, run.Serialization, run.Both) { }
		private ViewModel(string description, List<Result> instance, List<Result> serialization, List<Result> both)
		{
			this.description = description;
			this.instance = instance;
			this.serialization = serialization;
			this.both = both;
		}

		public static ViewModel Create<T>(string description, Run<T> run, Func<T, List<Result>> extract)
		{
			return new ViewModel(description, extract(run.Instance), extract(run.Serialization), extract(run.Both));
		}
	}
}

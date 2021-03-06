﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Platform.Storage;
using ServiceStack.Text;

namespace SmartApp.Sample2.Continuous
{
    class Program
    {
        static void Main(string[] args)
        {
            const int seconds = 1;
            var data = LoadData();
            ShowData(data, true);
            while (true)
            {
                var nextOffcet = data.NextOffset;
                Thread.Sleep(seconds * 1000);
                IAppendOnlyStreamReader reader = new FileAppendOnlyStoreReader(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\Platform.Node\bin\Debug\store"));

                var records = reader.ReadAll(nextOffcet);
                bool emptyData = true;
                foreach (var dataRecord in records)
                {
                    data.NextOffset = dataRecord.NextOffset;
                    if (data.Distribution.ContainsKey(dataRecord.Data.Length))
                        data.Distribution[dataRecord.Data.Length]++;
                    else
                        data.Distribution[dataRecord.Data.Length] = 1;
                    emptyData = false;
                }

                if (!emptyData)
                {
                    ShowData(data, false);
                    SaveData(data.ToJson());
                }
            }
        }

        private static void ShowData(Sample2Data data, bool dumpData)
        {
            Console.Clear();
            if (dumpData)
                Console.WriteLine("Data from storage!!!");
            Console.WriteLine("Next offset: {0}", data.NextOffset);
            Console.WriteLine("Distribution:");
            foreach (var pair in data.Distribution)
            {
                Console.WriteLine("[{0}]: {1}", pair.Key, pair.Value);
            }
        }

        static Sample2Data LoadData()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "sample2.dat");

            if (!File.Exists(path))
                return new Sample2Data { NextOffset = 0, Distribution = new Dictionary<int, int>() };

            return File.ReadAllText(path).FromJson<Sample2Data>();
        }

        static void SaveData(string jsonData)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "sample2.dat");
            using (var sw = new StreamWriter(path, false))
            {
                sw.Write(jsonData);
            }
        }
    }

    public class Sample2Data
    {
        public long NextOffset { get; set; }

        public Dictionary<int, int> Distribution { get; set; }
    }
}

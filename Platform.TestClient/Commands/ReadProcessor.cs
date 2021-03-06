﻿using System;
using System.Linq;
using System.Text;
using Platform.Node;
using Platform.Storage;

namespace Platform.TestClient.Commands
{
    public class ReadProcessor : ICommandProcessor
    {
        readonly IAppendOnlyStreamReader _reader;

        public ReadProcessor(IAppendOnlyStreamReader reader)
        {
            _reader = reader;
        }

        public string Key { get { return "RA"; } }
        public string Usage { get { return "RA [<from-offset> <max-record-count>]"; } }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            var fromOffset = 0;
            int maxRecordCount = int.MaxValue;

            if (args.Length > 0)
            {
                if (args.Length > 2)
                {
                    context.Log.Info("More arguments: {0}", args.Length);
                    return false;
                }

                int.TryParse(args[0], out fromOffset);
                if (args.Length > 1)
                    int.TryParse(args[1], out maxRecordCount);
            }

            context.IsAsync();

            var result = _reader.ReadAll(fromOffset, maxRecordCount);
            var dataRecords = result as DataRecord[] ?? result.ToArray();
            context.Log.Info("Read {0} records{1}", dataRecords.Length, dataRecords.Length > 0 ? ":" : ".");
            foreach (var record in dataRecords)
            {
                context.Log.Info("  stream-id: {0}, data: {0}", record.Key, Encoding.UTF8.GetString(record.Data));
            }

            var nextOffset = dataRecords.Length > 0 ? dataRecords.Last().NextOffset : 0;
            context.Log.Info("Next stream offset: {0}", nextOffset);

            context.Completed();
            return true;
        }
    }
}

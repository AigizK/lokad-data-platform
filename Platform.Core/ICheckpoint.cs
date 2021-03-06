using System;
using System.IO;

namespace Platform
{
    public interface ICheckpoint : IDisposable
    {
        string Name { get; }
        void Write(long point);
        //void Flush();
        void Close();

        long Read();
        //long ReadNonFlushed();
    }

    public interface IAppendOnlyStore : IDisposable
    {
        void Append(string key, byte[] data, int expectedStreamVersion);
    }

    public sealed class FileAppendOnlyStore : IAppendOnlyStore
    {

        ICheckpoint _checkpoint;

        ILogger _logger = LogManager.GetLoggerFor<FileAppendOnlyStore>();

        BitWriter _dataBits;
        FileStream _dataStream;

        BitWriter _checkBits;
        FileStream _checkStream;


        public FileAppendOnlyStore(string name)
        {
            var path = name ?? "";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            _checkStream = new FileStream(Path.Combine(path, "stream.chk"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            if (_checkStream.Length != 8)
                _checkStream.SetLength(8);
            _checkBits = new BitWriter(_checkStream);


            var b = new byte[8];
            _checkStream.Read(b, 0, 8);

            var offset = BitConverter.ToInt64(b, 0);

            _dataStream = new FileStream(Path.Combine(path, "stream.dat"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            _dataStream.Seek(offset, SeekOrigin.Begin);
            _dataBits = new BitWriter(_dataStream);
        }

        public void Dispose()
        {
            _dataStream.Close();
            _checkStream.Close();
        }

        public void Append(string key, byte[] data, int expectedStreamVersion)
        {
            //_logger.Info("Write to storage");
            // write data
            _dataBits.Write(key);
            _dataBits.Write7BitInt(data.Length);
            _dataBits.Write(data);
            _dataStream.Flush(true);

            _checkStream.Seek(0, SeekOrigin.Begin);
            _checkBits.Write(_dataStream.Position);
            _checkStream.Flush(true);
        }

        sealed class BitWriter : BinaryWriter
        {
            public BitWriter(Stream output) : base(output) {}

            public void Write7BitInt(int value)
            {
                base.Write7BitEncodedInt(value);
            }
        }
    }

    
}
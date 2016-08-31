using System;
using System.IO;

namespace Proliferate
{
    /// <summary>
    /// A wrapper that prefixes each <see cref="Write"/> with the length of bytes in the write.
    /// </summary>
    public class PipeWriteWrapper : Stream
    {
        private bool _closed;
        private readonly Stream _wrappedStream;
        private readonly BinaryWriter _binaryWriter;

        public PipeWriteWrapper(Stream wrappedStream)
        {
            _wrappedStream = wrappedStream;
            _binaryWriter = new BinaryWriter(_wrappedStream);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            //var lengthBytes = BitConverter.GetBytes(2);
            _binaryWriter.Write(count);
            _wrappedStream.Write(buffer, offset, count);
        }

#region NotSupported members
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }
        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }
        public override long Length
        {
            get { throw new NotImplementedException(); }
        }
#endregion

        public override bool CanWrite
        {
            get { return true; }
        }
        public override bool CanRead
        {
            get { return false; }
        }
        public override bool CanSeek
        {
            get { return false; }
        }
        public override void Flush()
        {
            _wrappedStream.Flush();
        }
        public override void Close()
        {
            if (!_closed)
            {
                _closed = true;
                try
                {
                    _binaryWriter.Write(0);
                }
                catch (IOException)
                {
                    //Ignore any IOExcpetions from tring to write to a broken pipe.
                }
            }
            base.Close();
        }
    }

}

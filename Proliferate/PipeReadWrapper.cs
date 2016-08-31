using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Proliferate
{
    /// <summary>
    /// Wraps a stream but skips over any length prefix bytes added to write operations by
    /// the counterpart <see cref="PipeWriteWrapper"/>.
    /// </summary>
    public class PipeReadWrapper : Stream
    {
        public PipeReadWrapper(Stream wrappedStream)
        {
            _wrappedStream = wrappedStream;
            _binaryReader = new BinaryReader(_wrappedStream);
        }

        private readonly Stream _wrappedStream;
        private readonly BinaryReader _binaryReader;
        private bool _closed = false;
        private int _remainingBytesToRead = -1;

        public override int Read(byte[] buffer, int offset, int count)
        {
            //Make sure any changes to this method are copied over to ReadAsync.
            if (_closed)
                return 0;
            var remainingCountToFullfillReadRequest = count;
            var currentBufferOffset = offset;
            var totalRead = 0;
            while (remainingCountToFullfillReadRequest > 0)
            {
                if (CheckRemainingByteChunkSize() == 0)
                    break;
                var numForThisRead = Math.Min(remainingCountToFullfillReadRequest, _remainingBytesToRead);
                var numActuallyRead = _wrappedStream.Read(buffer, currentBufferOffset, numForThisRead);
                if (numActuallyRead < numForThisRead)
                    throw new InvalidOperationException("Expected to read " + numForThisRead.ToString() +
                            " bytes from the stream but only got " + numActuallyRead.ToString());
                currentBufferOffset += numActuallyRead;
                remainingCountToFullfillReadRequest -= numActuallyRead;
                _remainingBytesToRead -= numForThisRead;
                totalRead += numActuallyRead;
            }
            return totalRead;
        }

        public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            //This code should be a duplicate of Read except for using ReadAsync internally.
            if (_closed)
                return 0;
            var remainingCountToFullfillReadRequest = count;
            var currentBufferOffset = offset;
            var totalRead = 0;
            while (remainingCountToFullfillReadRequest > 0)
            {
                if (CheckRemainingByteChunkSize() == 0)
                    break;
                var numForThisRead = Math.Min(remainingCountToFullfillReadRequest, _remainingBytesToRead);
                var numActuallyRead = await _wrappedStream.ReadAsync(buffer, currentBufferOffset, numForThisRead, cancellationToken);
                if (numActuallyRead < numForThisRead)
                    throw new InvalidOperationException("Expected to read " + numForThisRead.ToString() +
                            " bytes from the stream but only got " + numActuallyRead.ToString());
                currentBufferOffset += numActuallyRead;
                remainingCountToFullfillReadRequest -= numActuallyRead;
                _remainingBytesToRead -= numForThisRead;
                totalRead += numActuallyRead;
            }
            return totalRead;
        }

        /// <summary>
        /// Strips the length prefix bytes from the stream and reports the remaining length of
        /// the current chunk.  "Chunk" refers to the series of bytes that fall between length prefix bytes.
        /// </summary>
        public int CheckRemainingByteChunkSize()
        {
            if (!_closed && _remainingBytesToRead <= 0)
            {
                _remainingBytesToRead = _binaryReader.ReadInt32();
                if (_remainingBytesToRead == 0)
                {
                    _closed = true;
                }
            }
            return _remainingBytesToRead;
        }

        /// <summary>
        /// Reads bytes from the stream until the end of the current chunk is reached and then returns them.
        /// </summary>
        public byte[] ReadToEndOfChunk()
        {
            if (!CanRead)
                throw new InvalidOperationException("The wrapper has been closed. Check CanRead before calling ReadToEndOfChunk.");
            var remainingBytes = CheckRemainingByteChunkSize();
            var bytes = new byte[remainingBytes];
            Read(bytes, 0, bytes.Length);
            return bytes;
        }

#region NotSupported members
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
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
            get { throw new NotSupportedException(); }
        }
        public override void Flush()
        {
            throw new NotSupportedException();
        }
#endregion

        public override bool CanWrite
        {
            get { return false; }
        }
        public override bool CanRead
        {
            get { return !_closed; }
        }
        public override bool CanSeek
        {
            get { return false; }
        }
        public override void Close()
        {
            _closed = true;
            base.Close();
        }
    }
}
namespace VTT.Util
{
    using System.IO;

    public class StreamView : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _offset;
        private readonly long _length;
        private long _position;

        public override bool CanRead => this._baseStream.CanRead;

        public override bool CanSeek => this._baseStream.CanSeek;

        public override bool CanWrite => this._baseStream.CanWrite;

        public override long Length => this._length;

        public override long Position { get => this._position; set => this._position = this.Seek(value, SeekOrigin.Begin); }

        public override void Flush() => this._baseStream.Flush();
        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = this._length - this._position;
            if (remaining <= 0)
            {
                return 0;
            }

            if (remaining < count)
            {
                count = (int)remaining;
            }

            int read = this._baseStream.Read(buffer, offset, count);
            this._position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                {
                    this._position = offset;
                    break;
                }

                case SeekOrigin.End:
                {
                    this._position = this._length + offset;
                    break;
                }

                case SeekOrigin.Current:
                {
                    this._position += offset;
                    break;
                }
            }

            if (this._position < 0)
            {
                this._position = 0;
            }

            if (this._position >= this._length)
            {
                this._position = this._length - 1;
            }

            this._position = this._baseStream.Seek(this._offset + this.Position, SeekOrigin.Begin) - this._offset;
            return this._position;
        }
        public override void SetLength(long value) => this._baseStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count)
        {
            long remaining = this._length - this._position;
            if (remaining <= 0)
            {
                return;
            }

            if (remaining < count)
            {
                count = (int)remaining;
            }

            this._baseStream.Write(buffer, offset, count);
            this._position += count;
        }
    }
}

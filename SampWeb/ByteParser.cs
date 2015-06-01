namespace SampWeb
{
    /// <summary>
    /// Byte parser.
    /// </summary>
    internal sealed class ByteParser
    {
        /// <summary>
        /// Field _bytes.
        /// </summary>
        private readonly byte[] _bytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="ByteParser"/> class.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        internal ByteParser(byte[] bytes)
        {
            _bytes = bytes;
            CurrentOffset = 0;
        }

        /// <summary>
        /// Gets the current offset.
        /// </summary>
        internal int CurrentOffset
        {
            get;
            private set;
        }

        /// <summary>
        /// Reads the line.
        /// </summary>
        /// <returns>ByteString instance.</returns>
        internal ByteString ReadLine()
        {
            ByteString result = null;

            for (var i = CurrentOffset; i < _bytes.Length; i++)
            {
                if (_bytes[i] == 10)
                {
                    var num = i - CurrentOffset;

                    if (num > 0 && _bytes[i - 1] == 13)
                    {
                        num--;
                    }

                    result = new ByteString(_bytes, CurrentOffset, num);

                    CurrentOffset = i + 1;

                    return result;
                }
            }

            if (CurrentOffset < _bytes.Length)
            {
                result = new ByteString(_bytes, CurrentOffset, _bytes.Length - CurrentOffset);
            }

            CurrentOffset = _bytes.Length;

            return result;
        }
    }
}

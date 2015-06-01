using System;
using System.Collections;
using System.Text;

namespace SampWeb
{
    /// <summary>
    /// ByteString class.
    /// </summary>
    internal sealed class ByteString
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ByteString"/> class.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        public ByteString(byte[] bytes, int offset, int length)
        {
            Bytes = bytes;

            if (Bytes != null && offset >= 0 && length >= 0 && offset + length <= Bytes.Length)
            {
                Offset = offset;
                Length = length;
            }
        }

        /// <summary>
        /// Gets the bytes.
        /// </summary>
        public byte[] Bytes
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the length.
        /// </summary>
        public int Length
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the offset.
        /// </summary>
        public int Offset
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a value indicating whether this instance is empty.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return Bytes == null || Length == 0;
            }
        }

        /// <summary>
        /// Gets the <see cref="System.Byte"/> at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>Byte of the index.</returns>
        public byte this[int index]
        {
            get
            {
                return Bytes[Offset + index];
            }
        }

        /// <summary>
        /// Gets the string.
        /// </summary>
        /// <returns>The string.</returns>
        public string GetString()
        {
            return GetString(Encoding.UTF8);
        }

        /// <summary>
        /// Gets the string.
        /// </summary>
        /// <param name="encoding">The encoding.</param>
        /// <returns>The string.</returns>
        public string GetString(Encoding encoding)
        {
            if (IsEmpty)
            {
                return string.Empty;
            }

            return encoding.GetString(Bytes, Offset, Length);
        }

        /// <summary>
        /// Gets the bytes.
        /// </summary>
        /// <returns>Byte array.</returns>
        public byte[] GetBytes()
        {
            var result = new byte[Length];

            if (Length > 0)
            {
                Buffer.BlockCopy(Bytes, Offset, result, 0, Length);
            }

            return result;
        }

        /// <summary>
        /// Gets index of the char.
        /// </summary>
        /// <param name="value">The char.</param>
        /// <returns>Index of the char.</returns>
        public int IndexOf(char value)
        {
            return IndexOf(value, 0);
        }

        /// <summary>
        /// Gets index of the char.
        /// </summary>
        /// <param name="value">The char.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>Index of the char.</returns>
        public int IndexOf(char value, int offset)
        {
            for (var i = offset; i < Length; i++)
            {
                if (this[i] == (byte)value)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Gets substring of the specified offset.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <returns>ByteString instance.</returns>
        public ByteString Substring(int offset)
        {
            return Substring(offset, Length - offset);
        }

        /// <summary>
        /// Gets substring the specified offset.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <returns>ByteString instance.</returns>
        public ByteString Substring(int offset, int length)
        {
            return new ByteString(Bytes, Offset + offset, length);
        }

        /// <summary>
        /// Returns ByteString array that contains the substrings in this instance that are delimited by elements of a specified Unicode character.
        /// </summary>
        /// <param name="separator">The separator.</param>
        /// <returns>ByteString array.</returns>
        public ByteString[] Split(char separator)
        {
            var arrayList = new ArrayList();

            var i = 0;

            while (i < Length)
            {
                var num = IndexOf(separator, i);

                if (num < 0)
                {
                    arrayList.Add(Substring(i));
                    break;
                }

                arrayList.Add(Substring(i, num - i));

                i = num + 1;

                while (i < Length && this[i] == (byte)separator)
                {
                    i++;
                }
            }

            var count = arrayList.Count;

            var array = new ByteString[count];

            for (var j = 0; j < count; j++)
            {
                array[j] = (ByteString)arrayList[j];
            }

            return array;
        }
    }
}

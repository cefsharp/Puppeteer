using System;
using System.Runtime.Serialization;

namespace CefSharp.Dom
{
    [Serializable]
    internal class BufferException : PuppeteerException
    {
        public BufferException()
        {
        }

        public BufferException(string message) : base(message)
        {
        }

        public BufferException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected BufferException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}

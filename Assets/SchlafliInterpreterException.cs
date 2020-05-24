using System;
using System.Runtime.Serialization;

namespace DimensionKing
{
    [Serializable]
    internal class SchlafliInterpreterException : Exception
    {
        public SchlafliInterpreterException()
        {
        }

        public SchlafliInterpreterException(string message) : base(message)
        {
        }

        public SchlafliInterpreterException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected SchlafliInterpreterException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.Exceptions
{
    public class ConflictException : Exception
    {
        public string Code { get; }

        public ConflictException(string message, string code = "conflict")
            : base(message)
        {
            Code = code;
        }
    }
}

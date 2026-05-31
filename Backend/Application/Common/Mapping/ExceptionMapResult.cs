using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Application.Common.Mapping
{
    public class ExceptionMapResult
    {
        public int StatusCode { get; set; }
        public string Title { get; set; } = "Error";
        public string ErrorCode { get; set; } = "unknown_error";
    }
}

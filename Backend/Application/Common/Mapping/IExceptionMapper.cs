using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.Mapping
{
    public interface IExceptionMapper
    {
        ExceptionMapResult Map(Exception ex);
    }
}

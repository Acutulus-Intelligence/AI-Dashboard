

namespace Application.Common.Mapping
{
    public interface IExceptionMapper
    {
        ExceptionMapResult Map(Exception ex);
    }
}

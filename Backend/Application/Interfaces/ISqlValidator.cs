namespace Application.Interfaces;

public interface ISqlValidator
{
    bool IsSelectOnly(string sql, out string? errorMessage);
}

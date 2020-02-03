namespace Improbable.Postgres
{
    public interface IPostgresOptions
    {
        string PostgresHost { get; set; }
        string PostgresUserName { get; set; }
        string PostgresPassword { get; set; }
        string PostgresDatabase { get; set; }
        string PostgresAdditionalOptions { get; set; }
    }
}

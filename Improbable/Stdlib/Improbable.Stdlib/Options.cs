namespace Improbable.Stdlib
{
    public interface IWorkerOptions
    {
        string? WorkerName { get; set; }

        string? LogFileName { get; set; }
    }

    public interface IReceptionistOptions : IWorkerOptions
    {
        string SpatialOsHost { get; set; }

        ushort SpatialOsPort { get; set; }
    }

    public interface ILocatorOptions : IWorkerOptions
    {
        string SpatialOsHost { get; set; }

        ushort SpatialOsPort { get; set; }

        bool UseInsecureConnection { get; set; }

        string DevToken { get; set; }

        string DisplayName { get; set; }

        string PlayerId { get; set; }

        string ProjectName { get; set; }
    }
}

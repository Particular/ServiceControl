namespace ServiceControl.Persistence.EFCore.Abstractions;

// One subclass per storage type, so a store only ever receives the settings it can act on and the
// configuration layer's validation is carried by the type rather than re-asserted at the point of use.
public abstract class BodyStorageSettings
{
    public const int DefaultMinCompressionSize = 4096;
    public const int DefaultMaxBodySizeToStore = 102400; // 100 kb

    public int MinCompressionSize { get; set; } = DefaultMinCompressionSize;
    public int MaxBodySizeToStore { get; set; } = DefaultMaxBodySizeToStore;
}

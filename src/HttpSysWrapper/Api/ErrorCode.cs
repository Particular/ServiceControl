namespace HttpApiWrapper.Api
{
    internal enum ErrorCode : uint
    {
        Success = 0,
        FileNotFound = 2,
        AlreadyExists = 183,
        InsufficientBuffer = 122,
        NoMoreItems = 259,
    }
}

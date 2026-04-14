namespace MLNET_API.Services;

public sealed class PredictionCoreUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

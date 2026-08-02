namespace Harnfig;

internal class HarnfigException(string message, Exception? innerException = null)
    : Exception(message, innerException);

internal sealed class ConfigurationException(string message, Exception? innerException = null)
    : HarnfigException(message, innerException);

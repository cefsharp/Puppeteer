using Newtonsoft.Json.Linq;

namespace CefSharp.Dom.Messaging
{
    internal class EvaluateHandleResponse
    {
        public EvaluateExceptionResponseDetails ExceptionDetails { get; set; }

        public RemoteObject Result { get; set; }
    }
}

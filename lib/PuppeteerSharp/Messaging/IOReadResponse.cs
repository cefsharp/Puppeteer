namespace CefSharp.Dom.Messaging
{
    internal class IOReadResponse
    {
        public bool Eof { get; set; }

        public string Data { get; set; }

        public bool Base64Encoded { get; set; }
    }
}

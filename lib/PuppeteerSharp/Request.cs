using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CefSharp.Dom.Messaging;
using Microsoft.Extensions.Logging;

namespace CefSharp.Dom
{
    /// <summary>
    /// Whenever the page sends a request, the following events are emitted by puppeteer's page:
    /// <see cref="IDevToolsContext.Request"/> emitted when the request is issued by the page.
    /// <see cref="IDevToolsContext.Response"/> emitted when/if the response is received for the request.
    /// <see cref="IDevToolsContext.RequestFinished"/> emitted when the response body is downloaded and the request is complete.
    ///
    /// If request fails at some point, then instead of <see cref="IDevToolsContext.RequestFinished"/> event (and possibly instead of <see cref="IDevToolsContext.Response"/> event), the <see cref="IDevToolsContext.RequestFailed"/> event is emitted.
    ///
    /// If request gets a 'redirect' response, the request is successfully finished with the <see cref="IDevToolsContext.RequestFinished"/> event, and a new request is issued to a redirected url.
    /// </summary>
    public class Request
    {
        private readonly DevToolsConnection _connection;
        private readonly bool _allowInterception;
        private readonly ILogger _logger;
        private bool _interceptionHandled;

        internal Request(
            DevToolsConnection connection,
            Frame frame,
            string interceptionId,
            bool allowInterception,
            RequestWillBeSentPayload e,
            List<Request> redirectChain)
        {
            _connection = connection;
            _allowInterception = allowInterception;
            _interceptionHandled = false;
            _logger = _connection.LoggerFactory.CreateLogger<Request>();

            RequestId = e.RequestId;
            InterceptionId = interceptionId;
            IsNavigationRequest = e.RequestId == e.LoaderId && e.Type == ResourceType.Document;
            Url = e.Request.Url;
            ResourceType = e.Type;
            Method = e.Request.Method;
            PostData = e.Request.PostData;
            Frame = frame;
            RedirectChainList = redirectChain;

            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var keyValue in e.Request.Headers)
            {
                Headers[keyValue.Key] = keyValue.Value;
            }

            FromMemoryCache = false;
        }

        /// <summary>
        /// Responsed attached to the request.
        /// </summary>
        /// <value>The response.</value>
        public Response Response { get; internal set; }

        /// <summary>
        /// Gets or sets the failure.
        /// </summary>
        /// <value>The failure.</value>
        public string Failure { get; internal set; }

        /// <summary>
        /// Gets or sets the request identifier.
        /// </summary>
        /// <value>The request identifier.</value>
        public string RequestId { get; internal set; }

        /// <summary>
        /// Gets or sets the interception identifier.
        /// </summary>
        /// <value>The interception identifier.</value>
        public string InterceptionId { get; internal set; }

        /// <summary>
        /// Gets or sets the type of the resource.
        /// </summary>
        /// <value>The type of the resource.</value>
        public ResourceType ResourceType { get; internal set; }

        /// <summary>
        /// Gets the frame.
        /// </summary>
        /// <value>The frame.</value>
        public Frame Frame { get; }

        /// <summary>
        /// Gets whether this request is driving frame's navigation
        /// </summary>
        public bool IsNavigationRequest { get; }

        /// <summary>
        /// Gets or sets the HTTP method.
        /// </summary>
        /// <value>HTTP method.</value>
        public HttpMethod Method { get; internal set; }

        /// <summary>
        /// Gets or sets the post data.
        /// </summary>
        /// <value>The post data.</value>
        public object PostData { get; internal set; }

        /// <summary>
        /// Gets or sets the HTTP headers.
        /// </summary>
        /// <value>HTTP headers.</value>
        public Dictionary<string, string> Headers { get; internal set; }

        /// <summary>
        /// Gets or sets the URL.
        /// </summary>
        /// <value>The URL.</value>
        public string Url { get; internal set; }

        /// <summary>
        /// A redirectChain is a chain of requests initiated to fetch a resource.
        /// If there are no redirects and the request was successful, the chain will be empty.
        /// If a server responds with at least a single redirect, then the chain will contain all the requests that were redirected.
        /// redirectChain is shared between all the requests of the same chain.
        /// </summary>
        /// <example>
        /// For example, if the website http://example.com has a single redirect to https://example.com, then the chain will contain one request:
        /// <code>
        /// var response = await devToolsContext.GoToAsync("http://example.com");
        /// var chain = response.Request.RedirectChain;
        /// Console.WriteLine(chain.Length); // 1
        /// Console.WriteLine(chain[0].Url); // 'http://example.com'
        /// </code>
        /// If the website https://google.com has no redirects, then the chain will be empty:
        /// <code>
        /// var response = await devToolsContext.GoToAsync("https://google.com");
        /// var chain = response.Request.RedirectChain;
        /// Console.WriteLine(chain.Length); // 0
        /// </code>
        /// </example>
        /// <value>The redirect chain.</value>
        public Request[] RedirectChain => RedirectChainList.ToArray();

        internal bool FromMemoryCache { get; set; }

        internal List<Request> RedirectChainList { get; }

        /// <summary>
        /// Continues request with optional request overrides. To use this, request interception should be enabled with <see cref="IDevToolsContext.SetRequestInterceptionAsync(bool)"/>. Exception is immediately thrown if the request interception is not enabled.
        /// If the URL is set it won't perform a redirect. The request will be silently forwarded to the new url. For example, the address bar will show the original url.
        /// </summary>
        /// <param name="overrides">Optional request overwrites.</param>
        /// <returns>Task</returns>
        public async Task ContinueAsync(Payload overrides = null)
        {
            // Request interception is not supported for data: urls.
            if (Url.StartsWith("data:", StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }
            if (!_allowInterception)
            {
                throw new PuppeteerException("Request Interception is not enabled!");
            }
            if (_interceptionHandled)
            {
                throw new PuppeteerException("Request is already handled!");
            }

            _interceptionHandled = true;

            try
            {
                var requestData = new FetchContinueRequestRequest
                {
                    RequestId = InterceptionId
                };
                if (overrides?.Url != null)
                {
                    requestData.Url = overrides.Url;
                }

                if (overrides?.Method != null)
                {
                    requestData.Method = overrides.Method.ToString();
                }

                if (overrides?.PostData != null)
                {
                    requestData.PostData = Convert.ToBase64String(Encoding.UTF8.GetBytes(overrides?.PostData));
                }

                if (overrides?.Headers?.Count > 0)
                {
                    requestData.Headers = HeadersArray(overrides.Headers);
                }

                await _connection.SendAsync("Fetch.continueRequest", requestData).ConfigureAwait(false);
            }
            catch (PuppeteerException ex)
            {
                // In certain cases, protocol will return error if the request was already canceled
                // or the page was closed. We should tolerate these errors
                _logger.LogError(ex.ToString());
            }
        }

        /// <summary>
        /// Fulfills request with given response. To use this, request interception should be enabled with <see cref="IDevToolsContext.SetRequestInterceptionAsync(bool)"/>. Exception is thrown if request interception is not enabled.
        /// </summary>
        /// <param name="response">Response that will fulfill this request</param>
        /// <returns>Task</returns>
        public async Task RespondAsync(ResponseData response)
        {
            if (Url.StartsWith("data:", StringComparison.Ordinal))
            {
                return;
            }

            if (!_allowInterception)
            {
                throw new PuppeteerException("Request Interception is not enabled!");
            }
            if (_interceptionHandled)
            {
                throw new PuppeteerException("Request is already handled!");
            }

            _interceptionHandled = true;

            var responseHeaders = new List<Header>();

            if (response.Headers != null)
            {
                foreach (var keyValuePair in response.Headers)
                {
                    if (keyValuePair.Value == null)
                    {
                        continue;
                    }

                    if (keyValuePair.Value is ICollection values)
                    {
                        foreach (var val in values)
                        {
                            responseHeaders.Add(new Header { Name = keyValuePair.Key, Value = val.ToString() });
                        }
                    }
                    else
                    {
                        responseHeaders.Add(new Header { Name = keyValuePair.Key, Value = keyValuePair.Value.ToString() });
                    }
                }

                if (!response.Headers.ContainsKey("content-length") && response.BodyData != null)
                {
                    responseHeaders.Add(new Header { Name = "content-length", Value = response.BodyData.Length.ToString(CultureInfo.CurrentCulture) });
                }
            }

            if (response.ContentType != null)
            {
                responseHeaders.Add(new Header { Name = "content-type", Value = response.ContentType });
            }

            try
            {
                await _connection.SendAsync("Fetch.fulfillRequest", new FetchFulfillRequest
                {
                    RequestId = InterceptionId,
                    ResponseCode = response.Status != null ? (int)response.Status : 200,
                    ResponseHeaders = responseHeaders.ToArray(),
                    Body = response.BodyData != null ? Convert.ToBase64String(response.BodyData) : null
                }).ConfigureAwait(false);
            }
            catch (PuppeteerException ex)
            {
                // In certain cases, protocol will return error if the request was already canceled
                // or the page was closed. We should tolerate these errors
                _logger.LogError(ex.ToString());
            }
        }

        /// <summary>
        /// Aborts request. To use this, request interception should be enabled with <see cref="IDevToolsContext.SetRequestInterceptionAsync(bool)"/>.
        /// Exception is immediately thrown if the request interception is not enabled.
        /// </summary>
        /// <param name="errorCode">Optional error code. Defaults to <see cref="RequestAbortErrorCode.Failed"/></param>
        /// <returns>Task</returns>
        public async Task AbortAsync(RequestAbortErrorCode errorCode = RequestAbortErrorCode.Failed)
        {
            // Request interception is not supported for data: urls.
            if (Url.StartsWith("data:", StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }
            if (!_allowInterception)
            {
                throw new PuppeteerException("Request Interception is not enabled!");
            }
            if (_interceptionHandled)
            {
                throw new PuppeteerException("Request is already handled!");
            }

            var errorReason = errorCode.ToString();

            _interceptionHandled = true;

            try
            {
                await _connection.SendAsync("Fetch.failRequest", new FetchFailRequest
                {
                    RequestId = InterceptionId,
                    ErrorReason = errorReason
                }).ConfigureAwait(false);
            }
            catch (PuppeteerException ex)
            {
                // In certain cases, protocol will return error if the request was already canceled
                // or the page was closed. We should tolerate these errors
                _logger.LogError(ex.ToString());
            }
        }

        private Header[] HeadersArray(Dictionary<string, string> headers)
            => headers?.Select(pair => new Header { Name = pair.Key, Value = pair.Value }).ToArray();
    }
}

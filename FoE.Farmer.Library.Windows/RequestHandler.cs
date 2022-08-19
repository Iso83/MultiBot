using System;
using CefSharp;

namespace FoE.Farmer.Library.Windows
{
    public class RequestHandler : CefSharp.Handler.RequestHandler
    {
        ResRequestHandler _hndResourceReq = new ResRequestHandler();
        protected override IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
        {
            return _hndResourceReq;
        }
    }

    public class ResRequestHandler : CefSharp.Handler.ResourceRequestHandler
    {
        protected override CefReturnValue OnBeforeResourceLoad(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
        {
            Uri url;
            if (Uri.TryCreate(request.Url, UriKind.Absolute, out url) == false)
                throw new Exception("Request to \"" + request.Url + "\" can't continue, not a valid URI");

            //NOTE: When executing the callback in an async fashion need to check to see if it's disposed
            if (!callback.IsDisposed)
            {
                using (callback)
                {
                    if (request.Method == "POST")
                    {
                        if (request.Url != null)
                        {
                            var uri = request.Url.ToString();
                            
                            if (uri.IndexOf("/json") != -1)
                            {
                                Requests.UserKey = uri.Substring(uri.IndexOf("json?h=") + "json?h=".Length);

                                foreach (var key in request.Headers.AllKeys)
                                    Requests.TemplateRequestHeader[key] = request.Headers[key];
                                
                                Requests.TemplateRequestHeader["Uri"] = uri;
                                Requests.TemplateRequestHeader["Referrer"] = request.ReferrerUrl;

                                MainPage.ShowCookiesAsync();
                            }
                        }
                    }
                }
            }

            return CefReturnValue.Continue;
        }

        protected override bool OnResourceResponse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response)
        {
            Uri url;
            if (Uri.TryCreate(request.Url, UriKind.Absolute, out url) == false)
                throw new Exception("Request to \"" + request.Url + "\" can't continue, not a valid URI");

            //NOTE: When executing the callback in an async fashion need to check to see if it's disposed
            if (request.Method == "POST")
            {
                if (request.Url != null)
                {
                    var uri = request.Url.ToString();

                    if (uri.IndexOf("/json") != -1)
                        Requests.UserKey = uri.Substring(uri.IndexOf("json?h=") + "json?h=".Length);
                }
            }

            return false;
        }
    }
}
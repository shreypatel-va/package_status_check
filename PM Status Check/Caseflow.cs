
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Linq;
using CFExtensions;
using System.Drawing.Imaging;
using System.Text;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System.Text.RegularExpressions;
using Serilog;


namespace CFExtensions
{
    public class DownloadException : Exception
    {
        public HttpResponseMessage? Response { get; set; }
        public DownloadException(string message) : base(message) { }

        public DownloadException(string message, HttpResponseMessage response) : base(message) 
        {
            Response = response;
        }

    }

    public static class StringExtensions
    {
        /// <summary>
        /// Compares string against a glob pattern
        /// </summary>
        /// <param name="text">The text</param>
        /// <param name="pattern">The glob pattern</param>
        /// <returns>boolean of whether a match exists</returns>
        public static bool Like(this string text, string pattern)
        {
            return new Regex(
                "^" + Regex.Escape(pattern).Replace(@"\*",  ".*").Replace(@"\?", ".") + "$",
                RegexOptions.Singleline | RegexOptions.IgnoreCase
            ).IsMatch(text);
        }
    }

    public static class StreamExtensions
    {
        public static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize, IProgress<string>? progress = null, CancellationToken cancellation = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.CanRead) throw new ArgumentException("Must be readable", nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (!destination.CanWrite) throw new ArgumentException("Must be writable", nameof(destination));
            if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize));

            var buffer = new byte[bufferSize];
            long totalBytes = 0;
            int bytesRead = 0;
            while ((bytesRead = await source.ReadAsync(buffer,0,buffer.Length,cancellation).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellation).ConfigureAwait(false);
                totalBytes += bytesRead;
                progress?.Report($"Downloaded {totalBytes} bytes");
            }
        }
    }
    public static class HttpClientExtensions
    {
        public static async Task DownloadAsync(this HttpClient client, string requestURI, Stream destination, IProgress<string>? progress = null, CancellationToken cancellation = default)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestURI);
            using (HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,cancellation).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode) throw new DownloadException($"Download failed with {response.StatusCode}", response);


                using (var download = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false))
                {
                    if (progress == null)
                    {
                        await download.CopyToAsync(destination);
                        return;
                    }
                    await download.CopyToAsync(destination, 81920, progress, cancellation);
                }
            }
        }
    }

    public static class CaseflowExtensions
    {
        public static void CopyPropertiesTo<T, TU>(this T source, TU dest)
        {
            var sourceProps = typeof(T).GetProperties().Where(x => x.CanRead).ToList();
            var destProps = typeof(TU).GetProperties().Where(x => x.CanWrite).ToList();

            foreach (var sourceProp in sourceProps)
            {
                if (destProps.Any(x => x.Name == sourceProp.Name))
                    destProps.FirstOrDefault(x => x.Name == sourceProp.Name)?.SetValue(dest, sourceProp.GetValue(source, null), null);
            }
        }
    }
    

}

namespace PM_Status_Check
{ 

    public class Caseflow
    {
        public readonly static ILogger CFLog = Log.ForContext<Caseflow>();
        public readonly static string CASEFLOW_HOST = Properties.Settings.Default.Caseflow;
        //static readonly HttpClient _httpClient = new HttpClient(new CaseflowHandler(new LoggingHandler()));
        static readonly HttpClient _httpClient = new HttpClient(new CaseflowHandler());

        public static async Task<JsonDocument> DistributionStatusJson(string distribution_id)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, CASEFLOW_HOST + $"/idt/api/v2/distributions/{distribution_id}");
            request.Headers.Add("Accept", "application/json");
            HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) throw new Exception();
            var textcontent = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(textcontent);
        /*
         * {
                "id": "cd04cc02-111d-4c0b-a293-ff60a1c4f486",
                "recipient": {
                "type": "person",
                "id": "1bab48f2-dcb5-48ff-9828-fc693aa71237",
                "first_name": "ERIC",
                "middle_name": "J.",
                "last_name": "BOOKER",
                "participant_id": null
                },
                "description": null,
                "communication_package_id": "1686952c-782b-4dbc-9e82-9f2c46f998a1",
                "destinations": [
                {
                  "type": "domesticAddress",
                  "id": "793b55f3-c7a5-403d-b8e1-05db4d36b7fa",
                  "status": "SUCCESS",
                  "cbcm_send_attempt_date": null,
                  "address_line_1": "ERIC J. BOOKER",
                  "address_line_2": "199 WOOD LN",
                  "address_line_3": null,
                  "address_line_4": null,
                  "address_line_5": null,
                  "address_line_6": null,
                  "treat_line_2_as_addressee": false,
                  "treat_line_3_as_addressee": false,
                  "city": "Pineland",
                  "state": "TX",
                  "postal_code": "75968-4409",
                  "country_name": null,
                  "country_code": "US"
                }
                ],
                "status": "SUCCESS",
                "sent_to_cbcm_date": "2024-11-20T17:25:31.018899"
                }
         * 
         */
        }

        public static async Task<string?> DistributionStatus(string distribution_id)
        {
            var json = await DistributionStatusJson(distribution_id);
            return JsonLookup(json.RootElement, "status").GetString();
        }

        public static string safeStringLookup(JsonElement data, string lookup)
        {
            try
            {
                var element = JsonLookup(data, lookup);
                return element.GetString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        public static JsonElement JsonLookup(JsonElement data, string lookup)
        {
            var arrayPath = lookup.Split('.');
            var workingNode = data;
            foreach (string pathItem in arrayPath)
            {
                if (workingNode.ValueKind == JsonValueKind.Array)
                {
                    int index = int.Parse(pathItem);
                    workingNode = workingNode[index];
                }
                else
                {
                    workingNode = workingNode.GetProperty(pathItem);
                }
            }
            return workingNode;
        }

    }

    public class CaseflowUserException : Exception
    {
        public CaseflowUserException(string message) : base(message) { }
    }

    internal class LoggingHandler : DelegatingHandler
    {
        internal LoggingHandler()
        {
            InnerHandler = new HttpClientHandler();
        }

        internal LoggingHandler(HttpMessageHandler inner)
        {
            InnerHandler = inner;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            
            Log.Debug($"--> Request - {DateTime.Now.ToString("G")}");
            Log.Debug($"{request.Method} {request.RequestUri}");
            foreach ( var item in request.Headers)
            {
                foreach( var value in item.Value )
                {
                    Log.Debug($"{item.Key}: {value}");
                }
            }
            // Can we do cookies somehow?
            if (request.Content != null)
            {
                foreach (var item in request.Content.Headers)
                {
                    foreach (var value in item.Value)
                        Log.Debug($"{item.Key}: {value}");
                }
                Log.Debug(await request.Content.ReadAsStringAsync());
               // Log.Debug(request.Content.ToString());
            }
            Log.Debug("");

            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            Log.Debug($"<-- Response - {DateTime.Now.ToString("G")}");
            Log.Debug($"{(int)response.StatusCode} {response.ReasonPhrase}");
            foreach (var item in response.Headers)
            {
                foreach (var value in item.Value )
                {
                    Log.Debug($"{item.Key}: {value}");
                }
            }
            foreach (var item in response.Content.Headers)
            {
                foreach (var value in item.Value)
                {
                    Log.Debug($"{item.Key}: {value}");
                }
            }
            // Cookies Separately?
            Log.Debug("");
            Log.Debug(await response.Content.ReadAsStringAsync());
            Log.Debug("");
            return response;
        }

    }

    public class CookieHandler : DelegatingHandler
    {
        private static readonly CookieContainer cookieContainer = new CookieContainer();
        public CookieHandler()
        {
            InnerHandler = new HttpClientHandler();
        }
        public CookieHandler(HttpMessageHandler inner)
        {
            InnerHandler = inner;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            
            foreach (Cookie requestCookie in cookieContainer.GetCookies(request.RequestUri))
            {
                request.Headers.Add("Cookie", $"{requestCookie.Name}={requestCookie.Value}");
            }
            
            
            var response = await base.SendAsync(request, cancellationToken);
            IEnumerable<string> cookies;
            if (response.Headers.TryGetValues("set-cookie", out cookies))
            {
                foreach (var c in cookies)
                {

                    //Log.Debug(c);
                    Cookie cookie;
                    var parts = c.Split(new[]  {';'});
                    //Log.Debug(parts[0]);
                    var named = parts[0].Split('=');
                    if (named.Length > 1)
                        cookie = new Cookie(named[0], named[1]);
                    else
                        cookie = new Cookie(named[0], "");
                    cookie.Domain = response.RequestMessage.RequestUri.Host;
                    cookie.Path = "/";

                    for (int i = 1; i < parts.Length; i++)
                    {
                        var details = parts[i].Split('=');
                        if (details.Length == 2)
                        {
                            if (details[0].Trim(' ').ToLower() == "path")
                            {
                                cookie.Path = details[1];
                            }
                            else if (details[0].Trim(' ').ToLower() == "domain")
                            {
                                cookie.Domain = details[1];
                            }
                        }
                    }
                    cookieContainer.Add(cookie);
                    
                }
            }
            return response;
        }
    }

    public class WebViewCookieHandler : DelegatingHandler
    {
        private static readonly CookieContainer cookieContainer = new CookieContainer();
        private static readonly WebView2 WebView = new WebView2();
        private static CoreWebView2Environment? WebView2Env = null;

        public WebViewCookieHandler()
        {
            InnerHandler = new HttpClientHandler();
        }
        public WebViewCookieHandler(HttpMessageHandler inner)
        {
            InnerHandler = inner;
        }
        private async System.Threading.Tasks.Task InitializeAsync()
        {
            if (WebView2Env == null)
            {
                var filePath = Path.Combine(PM_Status_Check.Files.GetDataDirectory(), @"IDT\WebMain");
                WebView2Env = await CoreWebView2Environment.CreateAsync(null, filePath);
            }
            await WebView.EnsureCoreWebView2Async(WebView2Env);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {

            await InitializeAsync();
            var cookieMgr = WebView.CoreWebView2.CookieManager;
            if (cookieMgr == null) return await base.SendAsync(request, cancellationToken);

            foreach (var cookie in await cookieMgr.GetCookiesAsync(request.RequestUri?.AbsoluteUri))
            {
                request.Headers.Add("Cookie", $"{cookie.Name}={cookie.Value}");
            }

            HttpRequestMessage copyRequest = new(request.Method, request.RequestUri.ToString());
            if (request.Headers != null)
            {
                foreach (var header in request.Headers) copyRequest.Headers.Add(header.Key, header.Value);
            }
            


            var response = await base.SendAsync(request, cancellationToken);

            if ( (response.StatusCode == HttpStatusCode.Redirect 
                && response.Headers.Any(
                    h => h.Key.ToLower() == "location" && h.Value.Any( 
                        v => v.ToLower() == $"{Caseflow.CASEFLOW_HOST.ToLower()}/login")
                    )
                )
                ||
                (response.StatusCode == HttpStatusCode.OK 
                && 
                    (   
                        request.RequestUri.ToString().ToLower().IndexOf($"{Caseflow.CASEFLOW_HOST.ToLower()}/login") >= 0
                        ||
                        request.RequestUri.ToString().ToLower().IndexOf("https://efolder.cf.ds.va.gov/login") >= 0
                     )
                )
                )
            {
                // Redirected to Login
                var popup = new WebPopup();
                var popupResult = await popup.CaseflowOneTime($"{Caseflow.CASEFLOW_HOST}/queue");
                if (popupResult == null) throw new Exception();

                request = copyRequest;
                request.Headers.Remove("Cookie");
               
                foreach (var cookie in await cookieMgr.GetCookiesAsync(request.RequestUri?.AbsoluteUri))
                {
                    request.Headers.Add("Cookie", $"{cookie.Name}={cookie.Value}");
                }
                // Retry
                response = await base.SendAsync(request, cancellationToken);

            }

            //IEnumerable<string> cookies;
            if (response.Headers.TryGetValues("set-cookie", out IEnumerable<string>? cookies))
            {
                foreach (var c in cookies)
                {


                    //Log.Debug(c);
                    Cookie cookie;
                    var parts = c.Trim().TrimEnd(';').Split(new[] { ';' },2);
                    var named = parts[0].Split(new[] {'='},2);
                    if (named.Length > 1)
                        cookie = new Cookie(named[0], named[1]);
                    else
                        cookie = new Cookie(named[0], string.Empty);

                    if (parts.Length > 1)
                    {
                        var eachItem = parts[1].Trim().Split(new[] {';'});
                        foreach (string[] aPart in eachItem.Select(p => p.Trim().Split(new[] { '=' }, 2)))
                        {
                            switch (aPart[0].ToLower())
                            {
                                case "domain":
                                    cookie.Domain = aPart[1];
                                    break;
                                case "secure":
                                    cookie.Secure = true;
                                    break;
                                case "httponly":
                                    cookie.HttpOnly = true;
                                    break;
                                case "path":
                                    cookie.Path = aPart[1];
                                    break;
                                case "expires":
                                    try
                                    {
                                        cookie.Expires = DateTime.Parse(aPart[1]);
                                    }
                                    catch
                                    {
                                    
                                    }
                                    break;
                            }
                        }
                    }

                    var coreCookie = cookieMgr.CreateCookieWithSystemNetCookie(cookie);
                    cookieMgr.AddOrUpdateCookie(coreCookie);

                }
            }
            return response;
        }
    }

    internal class CaseflowHandler : DelegatingHandler
    {
        private static readonly CookieContainer cookieJar = new CookieContainer();
        private static readonly HttpClientHandler _handler = new HttpClientHandler
        {
            CookieContainer = cookieJar,
            UseCookies = true,
            UseDefaultCredentials = true
            
        };
        

        private static readonly HttpClient _httpClient = new HttpClient(new CookieHandler(new LoggingHandler(_handler)));
        private static string? _token;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        internal CaseflowHandler()
        {
            InnerHandler = new HttpClientHandler();
        }
        internal CaseflowHandler(HttpMessageHandler inner)
        {
            InnerHandler = inner;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_token == null || _token.Length == 0)
            {
                await ObtainToken();
            }
            request.Headers.Add("token", _token);
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {

                var content = await response.Content.ReadAsStringAsync();
                if (content.ToLower().IndexOf("user must be") >= 0)
                    throw new CaseflowUserException("User must be attorney, judge, dispatch, or intake");
                _token = "";
                await ObtainToken(false);
                request.Headers.Remove("token");
                request.Headers.Add("token", _token);
                response = await base.SendAsync(request, cancellationToken);
            }
            return response;
        }

        
        internal static async Task<string> ObtainToken(bool LoadFromFile = true)
        {
            await _semaphore.WaitAsync();
            if (_token != null && _token.Length > 0)
            {
                _semaphore.Release();
                return _token;
            }
            if (LoadFromFile)
            {
                _token = await LoadTokenFromFile();
                if (!string.IsNullOrEmpty(_token))
                {
                    _semaphore.Release();
                    return _token;
                }
            }


            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://appeals.cf.ds.va.gov/idt/api/v1/token");
            request.Headers.Add("Accept", "application/json");
            HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) throw new Exception();

            var textcontent = await response.Content.ReadAsStringAsync();
            Log.Debug(textcontent);
            var jsoncontent = JsonDocument.Parse(textcontent);
            var one_time_key = jsoncontent.RootElement.GetProperty("one_time_key").GetString();
            _token = jsoncontent.RootElement.GetProperty("token").GetString();
            await ActivateOneTimeKey(one_time_key);
            await SaveTokenToFile(_token);
            _semaphore.Release();
            return _token;
            
        }

        private static async Task<string> LoadTokenFromFile()
        {
            try
            {
                return Encryption.GetRegistry("IDTSCFT");
            }
            catch
            {
                
            }

            return "";
        }

        private static async Task SaveTokenToFile(string token)
        {

            Encryption.SetRegistry("IDTSCFT", token);
            var writer = new Encryption();
            await writer.EncryptToFileAsync(token, Path.Combine(Files.GetAppDir(), "IDTS-Token.enc"));

            /*
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IDT");
            System.IO.Directory.CreateDirectory(filePath);
            filePath = Path.Combine(filePath, "IDTS-Token.txt");
            using (var writer = System.IO.File.CreateText(filePath))
            {
                await writer.WriteAsync(token);
            }
            */
        }
        
        private static async Task<bool> ActivateOneTimeKey(string one_time_key)
        {
            //HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"https://appeals.cf.ds.va.gov/idt/auth?one_time_key={one_time_key}");
            //request.Headers.Add("Accept", "application/json");
            //HttpResponseMessage response = await _httpClient.SendAsync(request);

            var webPop = new WebPopup();
            var outputString = await webPop.CaseflowOneTime($"https://appeals.cf.ds.va.gov/idt/auth?one_time_key={one_time_key}");

            Debug.Print(outputString);

            return true;

        }
        


        /*
         * 'https://github.com/department-of-veterans-affairs/caseflow-efolder/blob/master/spec/requests/sso_spec.rb#L40
           For some reason, this works with .net 4.7, but not .net 8.0.  Appears to be with the NTLM authentication
    
         */
        internal static async Task<bool> EnsureCaseflowCookies()
        {
            Uri requestUri = new Uri("https://efolder.cf.ds.va.gov/login");
            var requestContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("station_id", "101")
            });
            var response = await _httpClient.PostAsync(requestUri, requestContent);

            requestUri = new Uri("https://ssologon.iam.va.gov/centrallogin/core/IWA/redirect.aspx");
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("Referer", response.RequestMessage.RequestUri.AbsoluteUri.ToString());
            
            response = await _httpClient.SendAsync(request);

            var responseContent = await response.Content.ReadAsStringAsync();

            var redirectLocation = responseContent.IndexOf("'HTTPS://logon.iam.va.gov/affwebservices/redirectjsp/redirect.jsp", StringComparison.CurrentCultureIgnoreCase);
            if (redirectLocation == -1)
                throw new Exception();
            
            var endLocation = responseContent.IndexOf("'", redirectLocation + 10);
            string redirectString = responseContent.Substring(redirectLocation + 1, endLocation - (redirectLocation + 1));
            response = await _httpClient.GetAsync(redirectString);
            responseContent = await response.Content.ReadAsStringAsync();

            redirectLocation = responseContent.IndexOf("<form action=", StringComparison.CurrentCultureIgnoreCase);
            if (redirectLocation == -1) throw new Exception();
            redirectLocation = responseContent.IndexOf('"', redirectLocation);
            if (redirectLocation == -1) throw new Exception();
            endLocation = responseContent.IndexOf('"', redirectLocation + 1);
            redirectString = responseContent.Substring(redirectLocation + 1, endLocation - (redirectLocation + 1));

            var inputLocation = responseContent.IndexOf("<input type=\"hidden\"", redirectLocation, StringComparison.CurrentCultureIgnoreCase);
            if (inputLocation == -1) throw new Exception();

            inputLocation = responseContent.IndexOf("value=", inputLocation, StringComparison.CurrentCultureIgnoreCase);
            inputLocation = responseContent.IndexOf('"', inputLocation);
            var inputEndLocation = responseContent.IndexOf('"', inputLocation + 1);
            if (inputEndLocation == -1) throw new Exception();
            var inputString = responseContent.Substring(inputLocation + 1, inputEndLocation - (inputLocation + 1));
            
            requestUri = new Uri(redirectString);
            requestContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("SAMLResponse", inputString)
            });
            response = await _httpClient.PostAsync(requestUri, requestContent);

            return true;

        }

        
    }
    
    
}


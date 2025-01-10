
using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace PM_Status_Check
{
    public static class GraphHelper
    {

        public const string Site_DecInn = "dvagov.sharepoint.com,b452e981-f057-4777-a475-0dc43cb2f4b0,3ced84a2-1cb1-4caa-b86f-289ee29be4aa";
        public const string List_ApplicationAccess = "36b9667c-3b5c-43fe-80bf-60506111fecc";

        public const string Site_Dispatch = "dvagov.sharepoint.com,cc670bca-05af-4c94-841f-242f69030019,a6fb2aff-48e9-4007-86ad-482d4aa01776";
        public const string List_Dispatch = "4b62727d-d150-407d-8501-6b7a83ea2ae9";

        public const string Site_BoardDecisionOutput = "dvagov.sharepoint.com,7bed3f2e-09ca-47b8-b9b1-5529b3e263e5,64d0b087-d0db-45ec-aaa9-6dec973c17f3";
        public const string List_Attorneys = "b9e9ecbb-97b1-4099-b8dc-cccfc5bbd4b5";

        public const string Site_Correspondence = "dvagov.sharepoint.com,9c702ce9-85d6-435f-b8fa-635662b92b69,aadab29b-1975-48ff-9e00-ba7747fbd5a2";
        public const string List_PMStatus = "3d7562c5-7453-4693-8fb0-98e9e2dd005d";
        public static GraphServiceClient Client { get; private set; }

        public static Microsoft.Graph.Models.User Me { get; private set; } = null;

        private static readonly string TenantId = "e95f1b23-abaf-45ee-821d-b7ab251ab3bf";
        private static readonly string ClientId = "a77a79fd-1fa6-44b2-a80e-10c7fedf1966";

        /*
        static GraphHelper()
        {

            var authenticationProvider = new BaseBearerTokenAuthenticationProvider(new TokenProvider(ClientId, TenantId));
            Client = new GraphServiceClient(authenticationProvider);

        }
        */
        static GraphHelper()
        {
            var scopes = new[] { "User.Read", "allsites.write", "sites.readwrite.all", "files.readwrite.all" };

            var serialized = Encryption.GetRegistry("graph_record");
            if (!string.IsNullOrWhiteSpace(serialized))
            {
                byte[] byteArray = Encoding.UTF8.GetBytes(serialized);
                MemoryStream stream = new MemoryStream(byteArray);
                var recordCache = AuthenticationRecord.Deserialize(stream);

                var options = new InteractiveBrowserCredentialOptions
                {
                    TenantId = TenantId,
                    ClientId = ClientId,
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                    RedirectUri = new Uri("http://localhost"),
                    TokenCachePersistenceOptions = new TokenCachePersistenceOptions(),
                    AuthenticationRecord = recordCache,
                };
                var interactiveCredential = new InteractiveBrowserCredential(options);
                Client = new GraphServiceClient(interactiveCredential, scopes);
            }
            else
            {
                var options = new InteractiveBrowserCredentialOptions
                {
                    TenantId = TenantId,
                    ClientId = ClientId,
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                    RedirectUri = new Uri("http://localhost"),
                    TokenCachePersistenceOptions = new TokenCachePersistenceOptions(),
                };
                var interactiveCredential = new InteractiveBrowserCredential(options);
                var context = new TokenRequestContext(scopes);
                var record = interactiveCredential.Authenticate(context);

                MemoryStream stream = new();
                record.Serialize(stream);
                stream.Position = 0;
                StreamReader sr = new(stream);
                string serializedToken = sr.ReadToEnd();
                Encryption.SetRegistry("graph_record", serializedToken);

                Client = new GraphServiceClient(interactiveCredential, scopes);
            }

        }


        public static async Task<Microsoft.Graph.Models.User> GetMe()
        {

            if (Me == null)
                Me = await Client.Me.GetAsync();
            return Me;
        }


        public static async Task<string> GetMyEmail()
        {
            return (await GetMe()).UserPrincipalName;
        }

        public static async Task<Microsoft.Graph.Models.DriveItem> DriveItemByPath(string drive_id, string path)
        {
            return await Client.Drives[drive_id].Root.ItemWithPath(path).GetAsync();
        }

        public static async Task<Microsoft.Graph.Models.DriveItem> DriveItemById(string drive_id, string id)
        {
            return await Client.Drives[drive_id].Items[id].GetAsync();
        }

        public static async Task<List<PackageStatus>> GetPMStatuses()
        {
            List<PackageStatus> packageStatuses = new List<PackageStatus>();

            var packageList = await GraphHelper.Client
                .Sites[GraphHelper.Site_Correspondence]
                .Lists[GraphHelper.List_PMStatus]
                .Items.GetAsync((config) =>
                {
                    config.QueryParameters.Expand = new string[] { "fields" };
                    //config.QueryParameters.Filter = $"(Fields/appeal_id) eq '{appealId}'";
                });

            var pageIterator = PageIterator<ListItem, ListItemCollectionResponse>
                .CreatePageIterator(
                    GraphHelper.Client,
                    packageList,
                    (listItem) =>
                    {
                        PackageStatus status = new();
                        object? output;
                        status.Id = listItem.Id;
                        if (listItem.Fields != null)
                        {
                            status.Title = listItem.Fields.AdditionalData.TryGetValue("Title", out output) ? output.ToString() : null;
                            status.DistributionId = listItem.Fields.AdditionalData.TryGetValue("DistributionId", out output) ? Convert.ToInt32(output) : null;
                            status.SentOn = listItem.Fields.AdditionalData.TryGetValue("SentOn", out output) ? Convert.ToDateTime(output) : null;
                            status.Status = listItem.Fields.AdditionalData.TryGetValue("Status", out output) ? output.ToString() : null;
                            status.FileNumber = listItem.Fields.AdditionalData.TryGetValue("FileNumber", out output) ? output.ToString() : null;
                            status.Username = listItem.Fields.AdditionalData.TryGetValue("Username", out output) ? output.ToString() : null;
                            status.LastChecked = listItem.Fields.AdditionalData.TryGetValue("LastChecked", out output) ? Convert.ToDateTime(output) : null;
                            status.Source = listItem.Fields.AdditionalData.TryGetValue("Source", out output) ? output.ToString() : null;
                            status.VBMSType = listItem.Fields.AdditionalData.TryGetValue("VBMSType", out output) ? output.ToString() : null;
                            status.VBMSSubject = listItem.Fields.AdditionalData.TryGetValue("VBMSSubject", out output) ? output.ToString() : null;
                            status.VBMSName = listItem.Fields.AdditionalData.TryGetValue("VBMSName", out output) ? output.ToString() : null;
                        }

                        packageStatuses.Add(status);
                        return true;

                    }
                );
            await pageIterator.IterateAsync();
            return packageStatuses;
        }

        public static async Task CheckCaseflowAndUpdate(PackageStatus status)
        {
            if (status.DistributionId.HasValue)
            {
                var newStatus = await Caseflow.DistributionStatus(status.DistributionId.Value.ToString());
                if (!string.IsNullOrEmpty(newStatus))
                {
                    status.Status = newStatus;
                    status.LastChecked = DateTime.Now;
                    await UpdatePMStatus(status, newStatus);
                    status.SyncStatus = "Synchronized";
                    Debug.WriteLine($"[CheckCaseflowAndUpdate Method] Synchronized {status.Id} || New Status {status.Status}");
                }
                else
                {
                    status.SyncStatus = "Caseflow Status Not Found";
                    Debug.WriteLine($"[CheckCaseflowAndUpdate Method] Not Synchronized {status.Id} || Old Status {status.Status}");
                }
            }
        }

        public static async Task<List<PackageStatus>> UpdatePMStatuses()
        {
            List<PackageStatus> packageStatuses = new List<PackageStatus>();

            var packageList = await GraphHelper.Client
                .Sites[GraphHelper.Site_Correspondence]
                .Lists[GraphHelper.List_PMStatus]
                .Items.GetAsync((config) =>
                {
                    config.QueryParameters.Expand = new string[] { "fields" };
                    config.QueryParameters.Filter = $"(Fields/Status) eq 'Created'";
                });

            var updatingList = new List<Task>();
            var pageIterator = PageIterator<ListItem, ListItemCollectionResponse>
                .CreatePageIterator(
                    GraphHelper.Client,
                    packageList,
                    async (listItem) =>
                    {
                        PackageStatus status = new();
                        object? output;
                        status.Id = listItem.Id;
                        if (listItem.Fields != null)
                        {
                            status.Title = listItem.Fields.AdditionalData.TryGetValue("Title", out output) ? output.ToString() : null;
                            status.DistributionId = listItem.Fields.AdditionalData.TryGetValue("DistributionId", out output) ? Convert.ToInt32(output) : null;
                            status.SentOn = listItem.Fields.AdditionalData.TryGetValue("SentOn", out output) ? Convert.ToDateTime(output) : null;
                            status.Status = listItem.Fields.AdditionalData.TryGetValue("Status", out output) ? output.ToString() : null;
                            status.FileNumber = listItem.Fields.AdditionalData.TryGetValue("FileNumber", out output) ? output.ToString() : null;
                            status.Username = listItem.Fields.AdditionalData.TryGetValue("Username", out output) ? output.ToString() : null;
                            status.LastChecked = listItem.Fields.AdditionalData.TryGetValue("LastChecked", out output) ? Convert.ToDateTime(output) : null;
                            status.Source = listItem.Fields.AdditionalData.TryGetValue("Source", out output) ? output.ToString() : null;
                            status.VBMSType = listItem.Fields.AdditionalData.TryGetValue("VBMSType", out output) ? output.ToString() : null;
                            status.VBMSSubject = listItem.Fields.AdditionalData.TryGetValue("VBMSSubject", out output) ? output.ToString() : null;
                            status.VBMSName = listItem.Fields.AdditionalData.TryGetValue("VBMSName", out output) ? output.ToString() : null;
                        }

                        updatingList.Add(CheckCaseflowAndUpdate(status));
                        //Debug.WriteLine(status.Id + " || " + status.Status);

                        if (updatingList.Count >= 5)
                        {
                            Debug.WriteLine("Synchronizing");
                            try
                            {
                                await Task.WhenAll(updatingList);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error during synchronization: {ex.Message}");
                            }
                            finally
                            {
                                Debug.WriteLine("Complete");
                                updatingList.Clear();
                            }
                        }
                        
                        Debug.WriteLine($"Found {status.Id}");

                        packageStatuses.Add(status);
                        return true;

                    }
                );
            await pageIterator.IterateAsync();
            if (updatingList.Count > 0) await Task.WhenAll(updatingList);
            return packageStatuses;
        }

        public static async Task<PackageStatus?> GetPackageStatus(string distributionId)
        {
            var itemSearch = await GraphHelper.Client
                .Sites[GraphHelper.Site_Correspondence]
                .Lists[GraphHelper.List_PMStatus]
                .Items.GetAsync(config =>
                {
                    config.QueryParameters.Expand = new string[] { "fields" };
                    config.QueryParameters.Filter = $"(Fields/DistributionId) eq '{distributionId}'";
                });
            if (itemSearch == null || itemSearch.Value == null || itemSearch.Value.First() == null)
            {
                return null;
            }
            ListItem listItem = itemSearch.Value.First();

            PackageStatus status = new();
            object? output;
            status.Id = listItem.Id;
            if (listItem.Fields != null)
            {
                status.Title = listItem.Fields.AdditionalData.TryGetValue("Title", out output) ? output.ToString() : null;
                status.DistributionId = listItem.Fields.AdditionalData.TryGetValue("DistributionId", out output) ? Convert.ToInt32(output) : null;
                status.SentOn = listItem.Fields.AdditionalData.TryGetValue("SentOn", out output) ? Convert.ToDateTime(output) : null;
                status.Status = listItem.Fields.AdditionalData.TryGetValue("Status", out output) ? output.ToString() : null;
                status.FileNumber = listItem.Fields.AdditionalData.TryGetValue("FileNumber", out output) ? output.ToString() : null;
                status.Username = listItem.Fields.AdditionalData.TryGetValue("Username", out output) ? output.ToString() : null;
                status.LastChecked = listItem.Fields.AdditionalData.TryGetValue("LastChecked", out output) ? Convert.ToDateTime(output) : null;
                status.Source = listItem.Fields.AdditionalData.TryGetValue("Source", out output) ? output.ToString() : null;
                status.VBMSType = listItem.Fields.AdditionalData.TryGetValue("VBMSType", out output) ? output.ToString() : null;
                status.VBMSSubject = listItem.Fields.AdditionalData.TryGetValue("VBMSSubject", out output) ? output.ToString() : null;
                status.VBMSName = listItem.Fields.AdditionalData.TryGetValue("VBMSName", out output) ? output.ToString() : null;
            }

            return status;

            //var item = itemSearch.AdditionalData.First().Value;


        }

        public static async Task UpdatePMStatus(PackageStatus packageStatus, string new_status)
        {
            var patchBody = new FieldValueSet
            {
                AdditionalData = new Dictionary<string, object>
                {
                    {
                        "Status", new_status
                    },
                    {
                        "LastChecked", DateTime.Now
                    }
                }
            };

            var result = await GraphHelper.Client
                .Sites[GraphHelper.Site_Correspondence]
                .Lists[GraphHelper.List_PMStatus]
                .Items[packageStatus.Id]
                .Fields.PatchAsync( patchBody );

        }


        public static async Task<List<Dispatch>> GetDispatchByAppeal(string appealId)
        {
            List<Dispatch> list = new List<Dispatch>();

            var dispatchList = await GraphHelper.Client
                .Sites[GraphHelper.Site_Dispatch]
                .Lists[GraphHelper.List_Dispatch]
                .Items.GetAsync((config) =>
                {
                    config.QueryParameters.Expand = new string[] { "fields" };
                    config.QueryParameters.Filter = $"(Fields/appeal_id) eq '{appealId}'";
                });





            var outputList = new List<Dispatch>();
            var pageIterator = PageIterator<ListItem, ListItemCollectionResponse>
                .CreatePageIterator(
                    GraphHelper.Client,
                    dispatchList,
                    (listItem) =>
                    {
                        Dispatch dispatch = new();
                        object? output;
                        dispatch.Id = listItem.Id;
                        if (listItem.Fields != null)
                        {
                            dispatch.AppealId = listItem.Fields.AdditionalData.TryGetValue("appeal_id", out output) ? output.ToString() : null;
                            dispatch.DocumentId = listItem.Fields.AdditionalData.TryGetValue("doc_id", out output) ? output.ToString() : null;
                            dispatch.DispatchStatus = listItem.Fields.AdditionalData.TryGetValue("dispatch_status", out output) ? output.ToString() : null;
                            dispatch.ProblemText = listItem.Fields.AdditionalData.TryGetValue("comments", out output) ? output.ToString() : null;
                            dispatch.Citation = listItem.Fields.AdditionalData.TryGetValue("citation", out output) ? output.ToString() : null;
                            dispatch.MailDate = listItem.Fields.AdditionalData.TryGetValue("mail_date", out output) ? Convert.ToDateTime(output) : null;
                            dispatch.Veteran = listItem.Fields.AdditionalData.TryGetValue("veteran", out output) ? output.ToString() : null;
                            dispatch.AppealInformation = listItem.Fields.AdditionalData.TryGetValue("appeal_information", out output) ? output.ToString() : null;
                            dispatch.ClaimId = listItem.Fields.AdditionalData.TryGetValue("claim_id", out output) ? output.ToString() : null;
                            dispatch.Comments = listItem.Fields.AdditionalData.TryGetValue("comments", out output) ? output.ToString() : null;
                            dispatch.DistributionIds = listItem.Fields.AdditionalData.TryGetValue("distribution_ids", out output) ? output.ToString() : null;

                        }

                        outputList.Add(dispatch);

                        return true;
                    }
                );
            await pageIterator.IterateAsync();
            return outputList;
        }

        public static async Task<string?> GetApplicationAccessItem(string application, string title)
        {
            var aaList = await GraphHelper.Client
                .Sites[GraphHelper.Site_DecInn]
                .Lists[GraphHelper.List_ApplicationAccess]
                .Items.GetAsync(req =>
                {
                    req.QueryParameters.Expand = new string[] { "fields" };
                    req.QueryParameters.Filter = $"(Fields/Application) eq '{application}' and (Fields/Title) eq '{title}'";
                });

            
            if (aaList?.Value == null || aaList.Value.Count != 1) return null;

            var aa = aaList.Value[0];

            if (aa == null || aa.Fields == null) return null;
            
            return aa.Fields.AdditionalData.TryGetValue("Value", out object? output) ? output.ToString() : null;

        }


        public static async Task<List<BoardDecisionsAttorney>> GetSharePointAttorneys()
        {
            var attorneyList = await GraphHelper.Client
                .Sites[GraphHelper.Site_BoardDecisionOutput]
                .Lists[GraphHelper.List_Attorneys]
                .Items.GetAsync((reqConfig) =>
                {
                    reqConfig.QueryParameters.Expand = new string[] { "fields" };
                });



            var outputList = new List<BoardDecisionsAttorney>();
            var pageIterator = PageIterator<ListItem, ListItemCollectionResponse>
                .CreatePageIterator(
                    GraphHelper.Client,
                    attorneyList,
                    (listItem) =>
                    {
                        BoardDecisionsAttorney attorney = new();
                        object? output;
                        attorney.Id = listItem.Id;
                        if (listItem.Fields != null)
                        {
                            attorney.Title = listItem.Fields.AdditionalData.TryGetValue("Title", out output) ? output.ToString() : null;
                            attorney.Username = listItem.Fields.AdditionalData.TryGetValue("Username", out output) ? output.ToString() : null;
                            attorney.email = listItem.Fields.AdditionalData.TryGetValue("email", out output) ? output.ToString() : null;
                            attorney.Team = listItem.Fields.AdditionalData.TryGetValue("Team", out output) ? output.ToString() : null;
                            attorney.Attorney = listItem.Fields.AdditionalData.TryGetValue("AttorneyReport", out output) ? Convert.ToBoolean(output) : null;
                            attorney.Judge = listItem.Fields.AdditionalData.TryGetValue("JudgeReport", out output) ? Convert.ToBoolean(output) : null;
                            attorney.SharePointId = listItem.Fields.AdditionalData.TryGetValue("SharePointLookupId", out output) ? Convert.ToInt32(output) : null;
                        }
                        
                        outputList.Add(attorney);

                        return true;
                    }
                );
            await pageIterator.IterateAsync();
            return outputList;
        }


    }

    public class TokenProvider : IAccessTokenProvider
    {
        private readonly IPublicClientApplication _publicClientApplication;
        public TokenProvider(string clientId, string tenantId)
        {
            _publicClientApplication = PublicClientApplicationBuilder
                .Create(clientId)
                .WithTenantId(tenantId)
                .Build();
            AllowedHostsValidator = new AllowedHostsValidator();
        }

        public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string,object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
        {
            var scopes = new[] { "User.Read", "allsites.write", "sites.readwrite.all", "files.readwrite.all" };
            var result = await _publicClientApplication.AcquireTokenByIntegratedWindowsAuth(scopes).ExecuteAsync(cancellationToken);
            return result.AccessToken;
        }

        public AllowedHostsValidator AllowedHostsValidator { get; }
    }

    public class PackageStatus
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public int? DistributionId { get; set; }
        public DateTime? SentOn { get; set; }
        public string? Status { get; set; }
        public string? FileNumber { get; set; }
        public string? Username { get; set; }
        public DateTime? LastChecked { get; set; }
        public string? Source { get; set; }
        public string? VBMSType { get; set; }
        public string? VBMSSubject { get; set; }
        public string? VBMSName { get; set; }
        public string? SyncStatus { get; set; }

    }



    public class BoardDecisionsAttorney
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public int? SharePointId { get; set; }
        public int? VACOLS { get; set; }
        public string? Username { get; set; }
        public string? email { get; set; }
        public DateTime? StartDate { get; set; }
        public string? Team { get; set; }
        public bool? Attorney { get; set; }
        public bool? Judge { get; set; }

    }

    public class Dispatch
    {
        public string? Id { get; set; } = null;

        public string? AppealId { get; set; }
        public string? DocumentId { get; set; }
        public string? DispatchStatus { get; set; }
        public string? ProblemText { get; set; }
        public string? Citation { get; set; }
        public DateTime? MailDate { get; set; }
        public string? Veteran { get; set; }
        public string? AppealInformation { get; set; }
        public string? ClaimId { get; set; }
        public string? Comments { get; set; }
        public string? DistributionIds { get; set; }

        public static async Task<Dispatch?> PopulateAppeal(string appealId)
        {
            if (string.IsNullOrEmpty(appealId)) return null;


            var hits = await GraphHelper.GetDispatchByAppeal(appealId);

            return hits.FirstOrDefault();

        }
    }

}

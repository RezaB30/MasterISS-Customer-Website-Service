using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.Support
{
    [DataContract]
    public class GetSupportListRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public int? RowCount { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceGetSupportListRequest : BaseRequest<GetSupportListRequest, SHA1>
    {
        [DataMember]
        public GetSupportListRequest GetSupportList { get { return Data; } set { Data = value; } }
    }
}
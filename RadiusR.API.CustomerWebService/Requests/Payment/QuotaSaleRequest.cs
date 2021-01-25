using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.Payment
{
    [DataContract]
    public class QuotaSaleRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public int? PackageId { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceQuotaSaleRequest : BaseRequest<QuotaSaleRequest, SHA1>
    {
        [DataMember]
        public QuotaSaleRequest QuotaSaleParameters
        {
            get
            {
                return Data;
            }
            set
            {
                Data = value;
            }
        }
    }

}
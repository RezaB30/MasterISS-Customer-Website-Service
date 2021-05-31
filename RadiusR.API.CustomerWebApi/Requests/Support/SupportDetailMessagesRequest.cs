using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

/// <summary>
/// Summary description for SupportDetailMessagesRequest
/// </summary>
/// 

namespace RadiusR.API.CustomerWebService.Requests.Support
{
    [DataContract]
    public class SupportDetailMessagesRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public long? SupportId { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceSupportDetailMessagesRequest : BaseRequest<SupportDetailMessagesRequest, SHA1>
    {
        [DataMember]
        public SupportDetailMessagesRequest SupportDetailMessagesParameters
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

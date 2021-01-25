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
    public class SendSupportFileRequest
    {
        [DataMember]
        public byte[] FileBytes { get; set; }
        [DataMember]
        public long? SupportMessageId { get; set; }
        [DataMember]
        public long? SubscriptionId { get; set; }

    }
    [DataContract]
    public partial class CustoemrServiceSendSupportFileRequest : BaseRequest<SendSupportFileRequest, SHA1>
    {
        [DataMember]
        public SendSupportFileRequest SendSupportFile { get { return Data; } set { Data = value; } }
    }
}
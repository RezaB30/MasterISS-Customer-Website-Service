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
    public class SendSupportMessageRequest
    {
        [DataMember]
        public long? SubscriptionId { get; set; }
        [DataMember]
        public long? SupportId { get; set; }
        [DataMember]
        public string Message { get; set; }
        [DataMember]
        public int? SupportMessageType { get; set; }

    }
    [DataContract]
    public partial class CustomerServiceSendSupportMessageRequest : BaseRequest<SendSupportMessageRequest, SHA1>
    {
        [DataMember]
        public SendSupportMessageRequest SendSupportMessageParameters
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

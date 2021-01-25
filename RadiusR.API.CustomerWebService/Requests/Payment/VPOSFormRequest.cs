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
    public class VPOSFormRequest
    {
        [DataMember]
        public decimal? PayableAmount { get; set; }
        [DataMember]
        public long? SubscriptionId { get; set; }
        [DataMember]
        public string OkUrl { get; set; }
        [DataMember]
        public string FailUrl { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceVPOSFormRequest : BaseRequest<VPOSFormRequest, SHA1>
    {
        [DataMember]
        public VPOSFormRequest VPOSFormParameters
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
using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses
{
    [DataContract]
    public class ChangeSubClientResponse
    {
        [DataMember]
        public string ValidDisplayName { get; set; }
        [DataMember]
        public long ID { get; set; }
        [DataMember]
        public string SubscriberNo { get; set; }
        [DataMember]
        public IEnumerable<string> RelatedCustomers { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceChangeSubClientResponse : BaseResponse<ChangeSubClientResponse, SHA1>
    {
        public CustomerServiceChangeSubClientResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public ChangeSubClientResponse ChangeSubClientResponse { get { return Data; } set { Data = value; } }
    }
}
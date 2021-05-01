using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.Support
{
    [DataContract]
    public class SupportStatusResponse
    {
        [DataMember]
        public int Count { get; set; }
        [DataMember]
        //public IEnumerable<long> SupportRequestIds { get; set; }
        public long? StageId { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceSupportStatusResponse : BaseResponse<SupportStatusResponse, SHA1>
    {
        public CustomerServiceSupportStatusResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public SupportStatusResponse SupportStatusResponse { get { return Data; } set { Data = value; } }
    }
}
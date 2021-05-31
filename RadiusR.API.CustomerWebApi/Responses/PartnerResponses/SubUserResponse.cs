using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.PartnerResponses
{
    [DataContract]
    public class SubUserResponse
    {
        [DataMember]
        public string RequestedSubUserEmail { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceSubUserResponse : BaseResponse<SubUserResponse, SHA256>
    {
        public PartnerServiceSubUserResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public SubUserResponse SubUserResponse { get { return Data; } set { Data = value; } }
    }

}
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
    public class AddSubUserResponse : ResponseValidationBase
    {
        [DataMember]
        public string RequestedSubUserEmail { get; set; }
    }

    [DataContract]
    public partial class PartnerServiceAddSubUserResponse : BaseResponse<AddSubUserResponse, SHA256>
    {
        public PartnerServiceAddSubUserResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public AddSubUserResponse AddSubUserResponse { get { return Data; } set { Data = value; } }
    }
}
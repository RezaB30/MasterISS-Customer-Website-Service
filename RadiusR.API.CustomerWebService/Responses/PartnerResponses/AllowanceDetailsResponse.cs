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
    public class AllowanceDetailsResponse
    {
        [DataMember]
        public int AllowanceStateID { get; set; }
        [DataMember]
        public string AllowanceStateName { get; set; }
        [DataMember]
        public decimal Price { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceAllowanceDetailsResponse : BaseResponse<AllowanceDetailsResponse[], SHA256>
    {
        public PartnerServiceAllowanceDetailsResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public AllowanceDetailsResponse[] AllowanceDetailsResponse { get { return Data; } set { Data = value; } }
    }
}
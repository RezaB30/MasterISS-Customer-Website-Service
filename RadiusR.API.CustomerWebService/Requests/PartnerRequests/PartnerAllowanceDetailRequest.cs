using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.PartnerRequests
{
    [DataContract]
    public class PartnerAllowanceDetailRequest : PaginationRequest
    {
        [DataMember]
        public int? AllowanceCollectionID { get; set; } // partner collections customer setup tasks id
        [DataMember]
        public int? PartnerId { get; set; }
    }
    public partial class PartnerServiceAllowanceDetailRequest : BaseRequest<PartnerAllowanceDetailRequest, SHA256>
    {
        [DataMember]
        public PartnerAllowanceDetailRequest PartnerAllowanceDetailRequest { get { return Data; } set { Data = value; } }
    }
}
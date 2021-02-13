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
    public class PartnerAllowanceRequest : PaginationRequest
    {
        [DataMember]
        public int? PartnerId { get; set; }
        [DataMember]
        public short? AllowanceTypeId { get; set; } // sale , setup
    }
    [DataContract]
    public partial class PartnerServiceAllowanceRequest : BaseRequest<PartnerAllowanceRequest, SHA256>
    {
        [DataMember]
        public PartnerAllowanceRequest PartnerAllowanceRequest { get { return Data; } set { Data = value; } }
    }
}
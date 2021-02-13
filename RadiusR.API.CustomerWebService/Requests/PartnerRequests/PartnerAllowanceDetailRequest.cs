using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.PartnerRequests
{
    [DataContract]
    public class PartnerAllowanceDetailRequest : PaginationRequest
    {
        [DataMember]
        public int AllowanceCollectionID { get; set; } // partner collections id
        [DataMember]
        public int? PartnerId { get; set; }
        [DataMember]
        public short? AllowanceTypeId { get; set; } // sale , setup
    }
}
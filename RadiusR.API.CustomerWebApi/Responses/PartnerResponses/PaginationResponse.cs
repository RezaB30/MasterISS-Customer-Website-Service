using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.PartnerResponses
{
    [DataContract]
    public class PaginationResponse
    {
        [DataMember]
        public int TotalPageCount { get; set; }
    }
}
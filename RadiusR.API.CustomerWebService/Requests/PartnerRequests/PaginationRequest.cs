using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.PartnerRequests
{
    [DataContract]
    public class PaginationRequest
    {
        private int? _pageNo;
        [DataMember]
        public int? PageNo
        {
            get { return _pageNo; }
            set { _pageNo = value ?? 0; }
        }
        private int? _itemPerPage;
        [DataMember]
        public int? ItemPerPage
        {
            get { return _itemPerPage; }
            set { _itemPerPage = value ?? 10; }
        }

    }
}
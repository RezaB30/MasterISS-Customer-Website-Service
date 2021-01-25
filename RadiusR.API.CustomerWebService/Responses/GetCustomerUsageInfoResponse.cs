using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace RadiusR.API.CustomerWebService.Responses
{
    [DataContract]
    public class GetCustomerUsageInfoResponse
    {
        [DataMember]
        public DateTime Date { get; set; }
        [DataMember]
        public int? Month { get; set; }
        [DataMember]
        public int? Year { get; set; }
        [DataMember]
        public long TotalDownload { get; set; }
        [DataMember]
        public long TotalUpload { get; set; }
    }

}

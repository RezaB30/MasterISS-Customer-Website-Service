using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RadiusR.API.Test_Unit
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void AuthBtn_Click(object sender, EventArgs e)
        {
            var username = PartnerAuthUsernameBox.Text;
            var password = PartnerAuthPasswordBox.Text;
            var request = new GenericServiceSettings();
            PartnerServiceReference.PartnerServiceClient client = new PartnerServiceReference.PartnerServiceClient();
            var response = client.Authenticate(new PartnerServiceReference.PartnerServiceAuthenticationRequest()
            {
                AuthenticationParameters = new PartnerServiceReference.AuthenticationRequest()
                {
                    PartnerPasswordHash = HashUtilities.CalculateHash<SHA256>(password),
                    UserEmail = username,
                    SubUserEmail = null
                },
                Culture = "tr-tr",
                Hash = request.Hash,
                Rand = request.Rand,
                Username = request.Username
            });
            MessageBox.Show(response.ResponseMessage.ErrorMessage);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            PartnerServiceReference.PartnerServiceClient client = new PartnerServiceReference.PartnerServiceClient();
            var request = new GenericServiceSettings();
            var response = client.AddSubUser(new PartnerServiceReference.PartnerServiceAddSubUserRequest()
            {
                AddSubUserRequestParameters = new PartnerServiceReference.AddSubUserRequest()
                {
                    RequestedSubUserEmail = "onr@onr.com",
                    RequestedSubUserName = "onur",
                    RequestedSubUserPassword = "123123",
                    UserEmail = "test@test.com",
                    SubUserEmail = "test@test.com",
                },
                Culture = "tr-tr",
                Hash = request.Hash,
                Rand = request.Rand,
                Username = request.Username
            });
            var aaa = response; 
        }

        private void button2_Click(object sender, EventArgs e)
        {
            PartnerServiceReference.PartnerServiceClient client = new PartnerServiceReference.PartnerServiceClient();
            var request = new GenericServiceSettings();
            var response = client.BillsBySubscriberNo(new PartnerServiceReference.PartnerServiceBillListRequest()
            {
                BillListRequest = new PartnerServiceReference.BillListRequest()
                {
                    SubscriberNo = "2555456163",
                    SubUserEmail = "onr@onr.com",
                    UserEmail = "test@test.com"
                },
                Culture = "tr-tr",
                Hash = request.Hash,
                Rand = request.Rand,
                Username = request.Username
            });
            var assd = response;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            PartnerServiceReference.PartnerServiceClient client = new PartnerServiceReference.PartnerServiceClient();
            var request = new GenericServiceSettings();
            var response = client.IDCardValidation(new PartnerServiceReference.PartnerServiceIDCardValidationRequest()
            {
                IDCardValidationRequest = new PartnerServiceReference.IDCardValidationRequest()
                {
                    BirthDate = "1995-09-08",
                    FirstName = "ONUR",
                    IDCardType = 2,
                    LastName = "CİVANOĞLU",
                    TCKNo = "55147390880",
                    RegistirationNo = "K13628202"
                },
                Culture = "tr-tr",
                Hash = request.Hash,
                Rand = request.Rand,
                Username = request.Username
            });
            var assd = response;
        }
    }
    public class GenericServiceSettings
    {        
        public string Culture { get; set; }
        public string Rand { get; set; }
        public string Username { get; set; }
        private string Password { get; set; }
        public GenericServiceSettings()
        {
            Culture = Thread.CurrentThread.CurrentUICulture.Name;
            Rand = Guid.NewGuid().ToString("N");
            Username ="testwebservice";
            Password = "12345678";
        }
        PartnerServiceReference.PartnerServiceClient client = new PartnerServiceReference.PartnerServiceClient();
        public string Hash { get { return HashUtilities.CalculateHash<SHA256>(Username + Rand + HashUtilities.CalculateHash<SHA256>(Password) + client.GetKeyFragment(Username)); } }
    }
    public static class HashUtilities
    {
        public static string CalculateHash<HAT>(string value) where HAT : HashAlgorithm
        {
            HAT algorithm = (HAT)HashAlgorithm.Create(typeof(HAT).Name);
            var calculatedHash = string.Join(string.Empty, algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b => b.ToString("x2")));
            return calculatedHash;
        }
    }
}

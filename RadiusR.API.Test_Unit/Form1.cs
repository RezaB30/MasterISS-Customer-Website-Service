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
                    PartnerUsername = username,
                    SubUserEmail = null
                },
                Culture = "tr-tr",
                Hash = request.Hash,
                Rand = request.Rand,
                Username = request.Username
            });
            MessageBox.Show(response.ResponseMessage.ErrorMessage);
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

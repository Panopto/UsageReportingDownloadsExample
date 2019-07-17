﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.ServiceModel;
using System.Text;

namespace UsageReportingDownloads
{
    class Program
    {
        private static string ServiceEndpoint = string.Format(
            "https://{0}/Panopto/PublicAPI/4.0/UsageReporting.svc",
            Properties.Settings.Default.HostAddress);

        static void Main(string[] args)
        {
            if(args.Length != 4)
            {
                PrintUsageAndExit();
            }

            string username = args[0];
            string password = args[1];
            string reportIdLiteral = args[2];
            string destinationPath = args[3];

            Guid reportId;
            if(!Guid.TryParse(reportIdLiteral, out reportId))
            {
                PrintUsageAndExit();
            }

            var auth = Program.GetAuthenticationInfo(username, password);
            System.IO.File.WriteAllLines(destinationPath, ReadReport(auth, reportId));
        }

        private static UsageReporting.AuthenticationInfo GetAuthenticationInfo(string username, string password)
        {
            return new UsageReporting.AuthenticationInfo
            {
                UserKey = username,
                Password = password
            };
        }

        /// <summary>
        /// Unused for now in this example; but good for managing usage reports generally.
        /// </summary>
        private static UsageReporting.UsageReportingClient GetClient()
        {
            return new UsageReporting.UsageReportingClient(
                endpointConfigurationName: "BasicHttpBinding_IUsageReporting",
                remoteAddress: new EndpointAddress(Program.ServiceEndpoint));
        }

        private static string GetReportRequestSoapEnvelope(UsageReporting.AuthenticationInfo auth, Guid reportId)
        {
            return string.Format(@"
                <s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
                    <s:Body>
                        <GetReport xmlns=""http://tempuri.org/"">
                            <auth
                                xmlns:a=""http://schemas.datacontract.org/2004/07/Panopto.Server.Services.PublicAPI.V40""
                                xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"">
                                    <a:AuthCode i:nil=""true""/>
                                        <a:Password>{1}</a:Password>
                                        <a:UserKey>{0}</a:UserKey>
                            </auth>
                            <reportId>{2}</reportId>
                        </GetReport>
                    </s:Body>
                </s:Envelope>",
                auth.UserKey,
                auth.Password,
                reportId);
        }

        /// <summary>
        /// The GetReport response is not a proper SOAP envelope.
        /// To consume it, we can't use a client as generated by the WSDL.
        /// Instead, we assemble our own SOAP request and process the response manually.
        /// </summary>
        /// <returns>The contents of the report, line by line.</returns>
        private static IEnumerable<string> ReadReport(UsageReporting.AuthenticationInfo auth, Guid reportId)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Program.ServiceEndpoint);
            request.Method = "POST";
            request.ContentType = "text/xml; charset=utf-8";
            request.Headers.Add("SoapAction", "http://tempuri.org/IUsageReporting/GetReport");

            var envelope = Encoding.UTF8.GetBytes(GetReportRequestSoapEnvelope(auth, reportId));
            request.ContentLength = envelope.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(envelope, 0, envelope.Length);
            }

            var response = (HttpWebResponse)request.GetResponse();

            string line;
            // ZipArchive comes from System.IO.Compression which must be added manually as a project reference
            using (var zipArchive = new ZipArchive(response.GetResponseStream()))
            // each report archive contains only a single file, so read the first one
            using (var zipStream = new StreamReader(zipArchive.Entries[0].Open()))
            {
                while ((line = zipStream.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        private static void PrintUsageAndExit()
        {
            Console.WriteLine("UsageReportingDownloads.exe <username> <password> <reportId> <destinationPath>");
            System.Environment.ExitCode = 0;
            System.Environment.Exit(System.Environment.ExitCode);
        }
    }
}

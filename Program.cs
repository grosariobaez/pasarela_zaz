using ECRti.Framework; // Ensure this namespace is correct and exists in the DLL
using System;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using System.Linq;

namespace EasyPOS_Cardnet
{
    class Program
    {
        private static string IpLocal = "192.168.10.50";
        private const int PortNumberLocal = 2018;
        private const int PortNumberRemote = 7060;
        private const int Timeout = 180000;
        private static bool keepRunning = true;
        private const string connectionString = "Server=192.168.10.50;Database=EasyPOS;User Id=sa;Password=1234;MultipleActiveResultSets=True;";
        
        static void Main(string[] args)
        {
            string IpRemote;
            string switch_on;
            if (args.Length < 2)
            {
                IpRemote = "All";
                switch_on = "Cierres";
                //Console.WriteLine("Favor proveer la direccion IP del Veriphone y el parametro de operacion (e.g., Ventas, Cierres, Cancelaciones).");
                //return;
            }
            else
            {
                IpRemote = args[0];
                switch_on = args[1];

            }
            IpLocal = Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? "No IPv4 address found.";
                    

            //Console.WriteLine(Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork));



            Console.WriteLine($"Recibiendo transacciones del POS{IpLocal} con la IP: {IpRemote} y operacion: {switch_on}");

            // Handle graceful shutdown
            Console.CancelKeyPress += (sender, e) => OnExit();
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => OnExit();

           


            // Main loop
            while (keepRunning)
            {
                try
                {
                    switch (switch_on)
                    {
                        case "Cierres":
                           // ProcessTransaction_Cierres(IpRemote);
                            //cierre_estatico(IpRemote);
                            Console.WriteLine($"Cerrando los lotes del Panel con la IP: {IpRemote} y operacion de : {switch_on}");
                            cierre_estatico("192.168.10.20");
                            cierre_estatico("192.168.10.21");
                            cierre_estatico("192.168.10.22");
                            cierre_estatico("192.168.10.23");
                            cierre_estatico("192.168.10.24");
                            cierre_estatico("192.168.10.25");
                            cierre_estatico("192.168.10.26");
                            cierre_estatico("192.168.10.27");
                            cierre_estatico("192.168.10.28");
                            cierre_estatico("192.168.10.29");
                            cierre_estatico("192.168.10.30");
                            Environment.Exit(0);
                            break;
                        case "Ventas":
                           ProcessSalesTransactionsSQLVer1(IpRemote);
                            break;
                        case "Cancelaciones":
                            //ProcessTransaction_Cancelations(IpRemote);
                            //ProcessCancelation("192.168.10.25",))

                            //IpRemote = args[0];
                            //switch_on = args[1];
                            //int HostCancelar = int.Parse(args[2]);
                            //int referencia_Cancelar = int.Parse(args[3]);
                            Console.WriteLine("Proceso de Cancelaciones.");
                            Console.Write("Porfavor Digite la IP del Panel: ");
                            IpRemote = Console.ReadLine();

                            Console.Write("Digite el No del Host(del 1 al 6): ");
                            int HostCancelar = int.Parse(Console.ReadLine());

                            Console.Write("Digite el numero de referencia: ");
                            int referencia_Cancelar = int.Parse(Console.ReadLine());

                            // Output message
                            Console.WriteLine("Iniciando cancelacion.");
                            Cancelacion_Directa(referencia_Cancelar, HostCancelar,IpRemote);
                            Console.WriteLine("Proceso de Cancelacion completada.");
                            Environment.Exit(0);
                            break;
                        default:
                            Console.WriteLine("Transaccion Invalida.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }

                //Thread.Sleep(3000);
            }
        }

        static void ProcessSalesTransactionsSQL(string IpRemote)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string sp_pos = "Procesar_POS"; 
                if (IpRemote == "All") {
                    sp_pos = "Procesar_POS_All";
                }
                else{
                    sp_pos = "Procesar_POS";
                }
                SqlCommand command = new SqlCommand(sp_pos, connection);
                command.CommandType = CommandType.StoredProcedure;
                
                if (IpRemote != "All")
                    command.Parameters.AddWithValue("@VERIFON", IpRemote);

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int transactionId = (int)reader["IDComunicacion"];
                        int amount = (int)reader["Monto"];
                        int discount = (int)reader["Otros"];
                        int tax = (int)reader["ITBIS"];
                        int itemCode = (int)reader["Factura"];
                        IpRemote = (string)reader["VERIFON"];

                        string Transac = (string)reader["Transaccion"];
                        int Cuotas = (int)reader["Cuotas"];

                        //if (Transac == "C200") {
                        //    ConsultaTransactionLast(transactionId, IpRemote);
                        //    return;
                        //} 
                        //else if (Transac == "C300") {
                        //    ProcessSingleTransactionCuotas(transactionId, amount, discount, tax, itemCode, IpRemote, Cuotas);   
                        //} else
                        ProcessSingleTransaction(transactionId, amount, discount, tax, itemCode, IpRemote);      
                        
                        
                    }
                }
            }
        }
        static void ProcessSalesTransactionsSQLVer1(string IpRemote)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string sp_pos = (IpRemote == "All") ? "Procesar_POS_All" : "Procesar_POS";

                    using (SqlCommand command = new SqlCommand(sp_pos, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        if (IpRemote != "All")
                        {
                            command.Parameters.AddWithValue("@VERIFON", IpRemote);
                        }

                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int transactionId = reader["IDComunicacion"] != DBNull.Value ? (int)reader["IDComunicacion"] : 0;
                                int amount = reader["Monto"] != DBNull.Value ? (int)reader["Monto"] : 0;
                                int discount = reader["Otros"] != DBNull.Value ? (int)reader["Otros"] : 0;
                                int tax = reader["ITBIS"] != DBNull.Value ? (int)reader["ITBIS"] : 0;
                                int itemCode = reader["Factura"] != DBNull.Value ? (int)reader["Factura"] : 0;
                                string verifon = reader["VERIFON"] as string ?? string.Empty;

                                string transactionType = reader["Transaccion"] as string ?? string.Empty;
                                int cuotas = reader["Cuotas"] != DBNull.Value ? (int)reader["Cuotas"] : 0;


                                if (transactionType == "C200")
                                {
                                    ConsultaTransactionLast(transactionId, verifon);
                                }
                                else if (transactionType == "C300")
                                {
                                    ProcessSingleTransactionCuotas(transactionId, amount, discount, tax, itemCode, verifon, cuotas);
                                }
                                else
                                {
                                    ProcessSingleTransactionVer1(transactionId, amount, discount, tax, itemCode, verifon);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing transactions: {ex.Message}");
                // Optionally, log the exception for debugging
            }
        }

        static void Cancelacion_transact(string host, string referenceno){
            //public string ProcessAnnulment(int host, int referenceNumber)

        }
        static void ProcessTransaction_Cancelations(string IpRemote)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand("dbo.Get_Cancelaciones", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@VERIFON", IpRemote);

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int idtrn = (int)reader["Id"];
                        int host = (int)reader["Host"];
                        int referencia = (int)reader["ReferenceNumber"];
                        ProcessCancelation(idtrn, host, referencia, IpRemote);
                    }
                }
            }
        }
    
        static void ProcessTransaction_Cierres(string IpRemote)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand("dbo.Get_Cierres", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@VERIFON", IpRemote);

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int idtrn = (int)reader["Id"];
                        ExecuteProcessClose(IpRemote, idtrn);
                    }
                }
            }
        }

        static void Cancelacion_Directa(int referencia, int host, string IpRemote)
        {
            Core core = new Core();

            var timeoutResult = core.SetTimeout(Timeout);
            Console.WriteLine(timeoutResult);

            var localEndPointResult = core.SetLocalEndPoint(IpLocal, PortNumberLocal);
            Console.WriteLine(localEndPointResult);

            var remoteEndPointResult = core.SetRemoteEndPoint(IpRemote, PortNumberRemote);
            Console.WriteLine(remoteEndPointResult);   

            var initialiceResult = core.Initialice();
            Console.WriteLine(initialiceResult);
            if (initialiceResult.Contains("Successful"))
            {
                Console.WriteLine($"Referencia cancelar: {referencia}");
                var response = core.ProcessAnnulment(host, referencia);
                dynamic jsonResponse = JsonConvert.DeserializeObject(response);
                Console.WriteLine($"Referencia: {referencia}, Response: {response}");
            }

        }

        static void ProcessCancelation(int idTrn, int host, int referencia, string IpRemote)
        {
            Core core = new Core();

            var timeoutResult = core.SetTimeout(Timeout);
            Console.WriteLine(timeoutResult);

            var localEndPointResult = core.SetLocalEndPoint(IpLocal, PortNumberLocal);
            Console.WriteLine(localEndPointResult);

            var remoteEndPointResult = core.SetRemoteEndPoint(IpRemote, PortNumberRemote);
            Console.WriteLine(remoteEndPointResult);    

            for (int i = 0; i < 3; i++)
            {
                var initialiceResult = core.Initialice();
                Console.WriteLine(initialiceResult);
                if (initialiceResult.Contains("Successful"))
                {
                    Console.WriteLine($"Referencia cancelar: {referencia}");
                    var response = core.ProcessAnnulment(host, referencia);
                    dynamic jsonResponse = JsonConvert.DeserializeObject(response);
                    SaveCancelationResults(idTrn, jsonResponse);
                    Console.WriteLine($"Referencia: {referencia}, Response: {response}");
                }
                if (i == 2) // Last attempt failed
                {
                    Console.WriteLine($"Failed to initialize after 3 attempts: {IpRemote}");
                    return;
                }
            }
        }

        static void SaveCancelationResults(int idtrn, dynamic response)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand("Voucher_SaveCanceledresult", connection);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.AddWithValue("@Id", idtrn);
                command.Parameters.AddWithValue("@ResultMessage", response);
                
                connection.Open();
                command.ExecuteNonQuery();

                Console.WriteLine($"Reference: {idtrn} processed");
            }
        }
        static void ProcessSingleTransactionCuotas(int transactionId, int amount, int discount, int tax, int itemCode, string IpRemote, int Cuotas)
        {
            Core core = new Core();

            var timeoutResult = core.SetTimeout(Timeout);
            Console.WriteLine(timeoutResult);

            var localEndPointResult = core.SetLocalEndPoint(IpLocal, PortNumberLocal);
            Console.WriteLine(localEndPointResult);

            var remoteEndPointResult = core.SetRemoteEndPoint(IpRemote, PortNumberRemote);
            Console.WriteLine(remoteEndPointResult);

            for (int i = 0; i < 3; i++)
            {
                var initialiceResult = core.Initialice();
                Console.WriteLine(initialiceResult);
                if (initialiceResult.Contains("Successful"))
                {
                    Console.WriteLine($"TransactionId: {transactionId}");
                    var response = core.ProcessDeferredSale(amount, discount, tax, itemCode, Cuotas);
                    Console.WriteLine($"TransactionId: {transactionId}, Response: {response}");

                    dynamic jsonResponse = JsonConvert.DeserializeObject(response);
                    if (jsonResponse == null)
                    {
                        Console.WriteLine("Failed to deserialize response. Response content might be invalid.");
                        return;
                    }
                    if (!response.StartsWith("{") || !response.EndsWith("}"))
                    {
                        Console.WriteLine("Invalid JSON format.");
                        return;
                    }

                    //string status = jsonResponse.Status
                    string status = jsonResponse?.Status ?? "Unknown";
                    //string product = jsonResponse.Card.Product;
                    string product = jsonResponse?.Card?.Product ?? "Unknown";
                    //string cardNumber = jsonResponse?.Card.CardNumber;
                    string cardNumber = jsonResponse?.Card?.CardNumber ?? "Unknown";
                    //string lote = jsonResponse.Batch;
                    string lote = jsonResponse?.Batch ?? "Unknown";
                    //string referencia = jsonResponse.Transaction.Reference;
                    string referencia = jsonResponse?.Transaction?.Reference ?? "Unknown";
                    //string authorizationNumber = jsonResponse.Transaction.AuthorizationNumber;
                    string authorizationNumber = jsonResponse?.Transaction?.AuthorizationNumber ?? "Unknown";
                    //string mode = jsonResponse.Mode.Value;
                    string mode = jsonResponse?.Mode?.Value ?? "Unknown";
                    //string rrn = jsonResponse.Transaction.RetrievalReference;
                    string rrn = jsonResponse?.Transaction?.RetrievalReference ?? "Unknown";
                    //string fechahora = jsonResponse.Transaction.DataTime;
                    string fechahora = jsonResponse?.Transaction?.DataTime ?? "Unknown";
                    //string appid = jsonResponse.Transaction.ApplicationIdentifier;
                    string appid = jsonResponse?.Transaction?.ApplicationIdentifier ?? "Unknown";
                    //string holderName = jsonResponse.Card.HolderName;
                    string holderName = jsonResponse?.Card?.HolderName ?? "Unknown";
                    //string terminalID = jsonResponse.TerminalID;
                    string terminalID = jsonResponse?.TerminalID ?? "Unknown";
                    //string merchantID = jsonResponse.MerchantID;
                    string merchantID = jsonResponse?.MerchantID ?? "Unknown";
                    //string acquired = jsonResponse.Acquired;
                    string acquired = jsonResponse?.Acquired ?? "Unknown";

                    //DCC
                    //string salesIndicator = jsonResponse.DynamicCurrencyConversion.SalesIndicator;
                    string salesIndicator = jsonResponse?.DynamicCurrencyConversion?.SalesIndicator ?? "Unknown";
                    //string calculationAccepted = jsonResponse.DynamicCurrencyConversion.CalculationAccepted;
                    string calculationAccepted = jsonResponse?.DynamicCurrencyConversion?.CalculationAccepted ?? "Unknown";
                    //string marginRate = jsonResponse.DynamicCurrencyConversion.MarginRate;
                    string marginRate = jsonResponse?.DynamicCurrencyConversion?.MarginRate ?? "Unknown";
                    //string amountdcc = jsonResponse.DynamicCurrencyConversion.Amount;
                    string amountdcc = jsonResponse?.DynamicCurrencyConversion?.Amount ?? "Unknown";
                    //string displayrate = jsonResponse.DynamicCurrencyConversion.DisplayRate;
                    string displayrate = jsonResponse?.DynamicCurrencyConversion?.DisplayRate ?? "Unknown";
                    //string transactioncurr = jsonResponse.DynamicCurrencyConversion.TransactionCurrency;
                    string transactioncurr = jsonResponse?.DynamicCurrencyConversion?.TransactionCurrency ?? "Unknown";
                    // DCC properties

                    SaveTransactionResultVer1(transactionId, status, product, cardNumber, lote, referencia, authorizationNumber, mode, rrn, fechahora, appid, holderName, terminalID, merchantID, acquired, response, salesIndicator, calculationAccepted, marginRate, amountdcc, displayrate, transactioncurr);

                    return;
                }
                if (i == 2) // Last attempt failed
                {
                    Console.WriteLine($"Failed to initialize after 3 attempts: {IpRemote}");
                    return;
                }
            }

        }

        static void ProcessSingleTransaction(int transactionId, int amount, int discount, int tax, int itemCode, string IpRemote)
        {
            Core core = new Core();

            var timeoutResult = core.SetTimeout(Timeout);
            Console.WriteLine(timeoutResult);

            var localEndPointResult = core.SetLocalEndPoint(IpLocal, PortNumberLocal);
            Console.WriteLine(localEndPointResult);

            var remoteEndPointResult = core.SetRemoteEndPoint(IpRemote, PortNumberRemote);
            Console.WriteLine(remoteEndPointResult);

            var initialiceResult = core.Initialice();
            Console.WriteLine(initialiceResult);

            if (initialiceResult.Contains("Successful"))
            {
                Console.WriteLine($"TransactionId: {transactionId}");
                var response = core.ProcessNormalSale(amount, discount, tax, itemCode);
                Console.WriteLine($"TransactionId: {transactionId}, Response: {response}");

                try
                {
                    dynamic jsonResponse = JsonConvert.DeserializeObject(response);
                    string status = jsonResponse.Status;
                    string product = jsonResponse.Card.Product;
                    string cardNumber = jsonResponse.Card.CardNumber;
                    string lote = jsonResponse.Batch;
                    string referencia = jsonResponse.Transaction.Reference;
                    string authorizationNumber = jsonResponse.Transaction.AuthorizationNumber;
                    string mode = jsonResponse.Mode.Value;
                    string rrn = jsonResponse.Transaction.RetrievalReference;
                    string fechahora = jsonResponse.Transaction.DataTime;
                    string appid = jsonResponse.Transaction.ApplicationIdentifier;
                    string holderName = jsonResponse.Card.HolderName;
                    string terminalID = jsonResponse.TerminalID;
                    string merchantID = jsonResponse.MerchantID;
                    string acquired = jsonResponse.Acquired;
                    //DCC
                    string salesIndicator = jsonResponse.DynamicCurrencyConversion.SalesIndicator;
                    string calculationAccepted = jsonResponse.DynamicCurrencyConversion.CalculationAccepted;
                    string marginRate = jsonResponse.DynamicCurrencyConversion.MarginRate;
                    string amountdcc = jsonResponse.DynamicCurrencyConversion.Amount;
                    string displayrate = jsonResponse.DynamicCurrencyConversion.DisplayRate;
                    string transactioncurr = jsonResponse.DynamicCurrencyConversion.TransactionCurrency;

                    SaveTransactionResult(transactionId, status, product, cardNumber, lote, referencia, authorizationNumber, mode, rrn, fechahora, appid, holderName, terminalID, merchantID, acquired, response, salesIndicator, calculationAccepted, marginRate, amountdcc, displayrate, transactioncurr);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed response DeserializeObject: {e.Message}");
                    //SaveTransactionResult(transactionId, "Failed", "", "000", "000", "000", "000", "", "", "", "", "", "", "", "000", response, "", "", "", "", "", "");
                }
            }
            else
            {
                Console.WriteLine($"Fallo de conexion con el POS:{IpRemote}");
               // SaveTransactionResult(transactionId, "Failed", "", "000", "000", "000", "000", "", "", "", "", "", "", "", "000", initialiceResult, "", "", "", "", "", "");
            }
        }
        static void ProcessSingleTransactionVer1(int transactionId, int amount, int discount, int tax, int itemCode, string IpRemote)
        {
            Core core = new Core();

            var timeoutResult = core.SetTimeout(Timeout);
            Console.WriteLine(timeoutResult);

            var localEndPointResult = core.SetLocalEndPoint(IpLocal, PortNumberLocal);
            Console.WriteLine(localEndPointResult);

            var remoteEndPointResult = core.SetRemoteEndPoint(IpRemote, PortNumberRemote);
            Console.WriteLine(remoteEndPointResult);

            for (int i = 0; i < 3; i++)
            {
                var initialiceResult = core.Initialice();
                Console.WriteLine(initialiceResult);
                if (initialiceResult.Contains("Successful"))
                {
                    Console.WriteLine($"TransactionId: {transactionId}");
                    var response = core.ProcessNormalSale(amount, discount, tax, itemCode);
                    Console.WriteLine($"TransactionId: {transactionId}, Response: {response}");

                    dynamic jsonResponse = JsonConvert.DeserializeObject(response);
                    if (jsonResponse == null)
                    {
                        Console.WriteLine("Failed to deserialize response. Response content might be invalid.");
                        return;
                    }
                    if (!response.StartsWith("{") || !response.EndsWith("}"))
                    {
                        Console.WriteLine("Invalid JSON format.");
                        return;
                    }

                    //string status = jsonResponse.Status
                    string status = jsonResponse?.Status ?? "Unknown";
                    //string product = jsonResponse.Card.Product;
                    string product = jsonResponse?.Card?.Product ?? "Unknown";
                    //string cardNumber = jsonResponse?.Card.CardNumber;
                    string cardNumber = jsonResponse?.Card?.CardNumber ?? "Unknown";
                    //string lote = jsonResponse.Batch;
                    string lote = jsonResponse?.Batch ?? "Unknown";
                    //string referencia = jsonResponse.Transaction.Reference;
                    string referencia = jsonResponse?.Transaction?.Reference ?? "Unknown";
                    //string authorizationNumber = jsonResponse.Transaction.AuthorizationNumber;
                    string authorizationNumber = jsonResponse?.Transaction?.AuthorizationNumber ?? "Unknown";
                    //string mode = jsonResponse.Mode.Value;
                    string mode = jsonResponse?.Mode?.Value ?? "Unknown";
                    //string rrn = jsonResponse.Transaction.RetrievalReference;
                    string rrn = jsonResponse?.Transaction?.RetrievalReference ?? "Unknown";
                    //string fechahora = jsonResponse.Transaction.DataTime;
                    string fechahora = jsonResponse?.Transaction?.DataTime ?? "Unknown";
                    //string appid = jsonResponse.Transaction.ApplicationIdentifier;
                    string appid = jsonResponse?.Transaction?.ApplicationIdentifier ?? "Unknown";
                    //string holderName = jsonResponse.Card.HolderName;
                    string holderName = jsonResponse?.Card?.HolderName ?? "Unknown";
                    //string terminalID = jsonResponse.TerminalID;
                    string terminalID = jsonResponse?.TerminalID ?? "Unknown";
                    //string merchantID = jsonResponse.MerchantID;
                    string merchantID = jsonResponse?.MerchantID ?? "Unknown";
                    //string acquired = jsonResponse.Acquired;
                    string acquired = jsonResponse?.Acquired ?? "Unknown";

                    //DCC
                    //string salesIndicator = jsonResponse.DynamicCurrencyConversion.SalesIndicator;
                    string salesIndicator = jsonResponse?.DynamicCurrencyConversion?.SalesIndicator ?? "Unknown";
                    //string calculationAccepted = jsonResponse.DynamicCurrencyConversion.CalculationAccepted;
                    string calculationAccepted = jsonResponse?.DynamicCurrencyConversion?.CalculationAccepted ?? "Unknown";
                    //string marginRate = jsonResponse.DynamicCurrencyConversion.MarginRate;
                    string marginRate = jsonResponse?.DynamicCurrencyConversion?.MarginRate ?? "Unknown";
                    //string amountdcc = jsonResponse.DynamicCurrencyConversion.Amount;
                    string amountdcc = jsonResponse?.DynamicCurrencyConversion?.Amount ?? "Unknown";
                    //string displayrate = jsonResponse.DynamicCurrencyConversion.DisplayRate;
                    string displayrate = jsonResponse?.DynamicCurrencyConversion?.DisplayRate ?? "Unknown";
                    //string transactioncurr = jsonResponse.DynamicCurrencyConversion.TransactionCurrency;
                    string transactioncurr = jsonResponse?.DynamicCurrencyConversion?.TransactionCurrency ?? "Unknown";
                    // DCC properties

                    SaveTransactionResultVer1(transactionId, status, product, cardNumber, lote, referencia, authorizationNumber, mode, rrn, fechahora, appid, holderName, terminalID, merchantID, acquired, response, salesIndicator, calculationAccepted, marginRate, amountdcc, displayrate, transactioncurr);

                    return;
                }
                if (i == 2) // Last attempt failed
                {
                    Console.WriteLine($"Failed to initialize after 3 attempts: {IpRemote}");
                    return;
                }
            }
        }

        static void SaveTransactionResult(int transactionId, string status, string product, string cardNumber, string lote, string referencia, string authorizationNumber, string mode, string rrn, string fechahora, string appid, string holderName, string terminalid, string merchantid, string acquired, string response, string salesIndicator, string calculationAccepted, string marginRate,string  amountdcc, string displayrate,string transactioncurr)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand("Procesa_POS_Res", connection);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.AddWithValue("@ID_Transaction", transactionId);
                command.Parameters.AddWithValue("@Aprobacion", authorizationNumber);
                command.Parameters.AddWithValue("@Estatus", status);
                command.Parameters.AddWithValue("@Product", product);
                command.Parameters.AddWithValue("@CardNumber", cardNumber);
                command.Parameters.AddWithValue("@Lote", lote);
                command.Parameters.AddWithValue("@Reference", referencia);
                command.Parameters.AddWithValue("@Mode", mode);
                command.Parameters.AddWithValue("@rnn", rrn);
                command.Parameters.AddWithValue("@fechahora", fechahora);
                command.Parameters.AddWithValue("@appid", appid);
                command.Parameters.AddWithValue("@holdername", holderName); 
                command.Parameters.AddWithValue("@terminalid", terminalid);
                command.Parameters.AddWithValue("@merchantid", merchantid);
                command.Parameters.AddWithValue("@acquired", acquired);
                command.Parameters.AddWithValue("@salesIndicator", salesIndicator);
                command.Parameters.AddWithValue("@calculationAccepted", calculationAccepted);
                command.Parameters.AddWithValue("@marginRate", marginRate);
                command.Parameters.AddWithValue("@amountdcc", amountdcc);
                command.Parameters.AddWithValue("@displayrate", displayrate);
                command.Parameters.AddWithValue("@transactioncurr", transactioncurr);
                command.Parameters.AddWithValue("@Trama_Recibida", response);

                connection.Open();
                command.ExecuteNonQuery();

                Console.WriteLine($"TransactionId: {transactionId} processed with status: {status}");
            }
        }

        static void SaveTransactionResultVer1(
            int transactionId, 
            string status, 
            string product, 
            string cardNumber, 
            string lote, 
            string referencia, 
            string authorizationNumber, 
            string mode, 
            string rrn, 
            string fechahora, 
            string appid, 
            string holderName, 
            string terminalid, 
            string merchantid, 
            string acquired, 
            string response, 
            string salesIndicator, 
            string calculationAccepted, 
            string marginRate, 
            string amountdcc, 
            string displayrate, 
            string transactioncurr)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    using (SqlCommand command = new SqlCommand("Procesa_POS_Res", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        // Add parameters with null checks
                        command.Parameters.AddWithValue("@ID_Transaction", transactionId);
                        command.Parameters.AddWithValue("@Aprobacion", authorizationNumber ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@Estatus", status ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@Product", product ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@CardNumber", cardNumber ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@Lote", lote ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@Reference", referencia ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@Mode", mode ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@rnn", rrn ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@fechahora", fechahora ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@appid", appid ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@holdername", holderName ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@terminalid", terminalid ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@merchantid", merchantid ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@acquired", acquired ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@salesIndicator", salesIndicator ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@calculationAccepted", calculationAccepted ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@marginRate", marginRate ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@amountdcc", amountdcc ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@displayrate", displayrate ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@transactioncurr", transactioncurr ?? DBNull.Value.ToString());
                        command.Parameters.AddWithValue("@Trama_Recibida", response ?? DBNull.Value.ToString());

                        // Execute the command
                        connection.Open();
                        command.ExecuteNonQuery();

                        // Log the result
                        Console.WriteLine($"TransactionId: {transactionId} processed with status: {status}");
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                Console.WriteLine($"Database error while processing transaction {transactionId}: {sqlEx.Message}");
                // Consider logging the full exception details to a file or monitoring system
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error while processing transaction {transactionId}: {ex.Message}");
            }
        }

        static void OnExit()
        {
            keepRunning = false;
            Console.WriteLine("Application is shutting down...");
        }
        public static void cierre_estatico(string ipterminal)
        {
            Core core = new Core();

            var timeoutResult = core.SetTimeout(Timeout);
            Console.WriteLine(timeoutResult);

            var localEndPointResult = core.SetLocalEndPoint(IpLocal, PortNumberLocal);
            Console.WriteLine(localEndPointResult);

            var remoteEndPointResult = core.SetRemoteEndPoint(ipterminal, PortNumberRemote);
            Console.WriteLine(remoteEndPointResult);

            for (int i = 0; i < 3; i++)
            {
                var initialiceResult = core.Initialice();
                Console.WriteLine(initialiceResult);
                if (initialiceResult.Contains("Successful"))
                {
                    Console.WriteLine($"Cerrando Lote Terminal: {ipterminal}, Controlador:{IpLocal}");
                    Console.WriteLine(core.ProcessClose());
                }
            }


        }

        public static void ExecuteProcessClose(string ipRemota, int idtrn)
        {
            Core core = new Core();

            var timeoutResult = core.SetTimeout(Timeout);
            Console.WriteLine(timeoutResult);

            var localEndPointResult = core.SetLocalEndPoint(IpLocal, PortNumberLocal);
            Console.WriteLine(localEndPointResult);

            var remoteEndPointResult = core.SetRemoteEndPoint(ipRemota, PortNumberRemote);
            Console.WriteLine(remoteEndPointResult);

            try
            {
                var initialiceResult = core.Initialice();
                Console.WriteLine(initialiceResult);

                if (initialiceResult.Contains("Successful"))
                {
                    Console.WriteLine(core.SetTimeout(Timeout));
                    Console.WriteLine(core.SetLocalEndPoint(IpLocal, PortNumberLocal));
                    Console.WriteLine(core.SetRemoteEndPoint(ipRemota, PortNumberRemote));
                    //Console.WriteLine(core.SetRemoteEndPoint("192.168.10.21", PortNumberRemote));

                    string closeResponse = core.ProcessClose();
                    //dynamic jsonResponse = JsonConvert.DeserializeObject(closeResponse);
                    //string status = jsonResponse.Status;
                    //string closureQuantity = jsonResponse.Closure.Quantity;
                    //string closure_Result = jsonResponse.Closures.Result;
                    //string closure_Host = jsonResponse.Closures.Host;
                    //string closure_Batch = jsonResponse.Closures.Batch;
                    //string closure_DataTime = jsonResponse.Closures.DataTime;
                    //string purchase_Quantity = jsonResponse.Purchases.Quantity;
                    //string purchase_Tax = jsonResponse.Purchases.Tax;
                    //string purchase_OtherTax = jsonResponse.Purchases.OtherTax;
                    //string return_Quantity = jsonResponse.Returns.Quantity;
                    //string return_Amount = jsonResponse.Returns.Amount;
                    //string return_Tax = jsonResponse.Returns.Tax;

                    LogCloseResponse(ipRemota, closeResponse, idtrn);
                    //LogCloseResponse("192.168.10.21", closeResponse, 3);
                }
                else
                {
                    LogCloseResponse(ipRemota, "error", idtrn);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during batch closure: {ex.Message}");
                LogCloseResponse(ipRemota, ex.Message, idtrn);
            }
        }

        static void ConsultaTransactionLast(int transactionId, string IpRemote)
        {
            Core core = new Core();

            var timeoutResult = core.SetTimeout(Timeout);
            Console.WriteLine(timeoutResult);

            var localEndPointResult = core.SetLocalEndPoint(IpLocal, PortNumberLocal);
            Console.WriteLine(localEndPointResult);

            var remoteEndPointResult = core.SetRemoteEndPoint(IpRemote, PortNumberRemote);
            Console.WriteLine(remoteEndPointResult);

            var initialiceResult = core.Initialice();
            Console.WriteLine(initialiceResult);

            if (initialiceResult.Contains("Successful"))
            {
                Console.WriteLine($"TransactionId: {transactionId}");
               var response = core.ProcessGetLastApprovedTransaction();// "" ;//core.ProcessLastApprovedTransaction();
                Console.WriteLine($"TransactionId: {transactionId}, Response: {response}");

                try
                {
                    dynamic jsonResponse = JsonConvert.DeserializeObject(response);
                    string status = jsonResponse.Status;
                    string product = jsonResponse.Card.Product;
                    string cardNumber = jsonResponse.Card.CardNumber;
                    string lote = jsonResponse.Batch;
                    string referencia = jsonResponse.Transaction.Reference;
                    string authorizationNumber = jsonResponse.Transaction.AuthorizationNumber;
                    string mode = jsonResponse.Mode.Value;
                    string rrn = jsonResponse.Transaction.RetrievalReference;
                    string fechahora = jsonResponse.Transaction.DataTime;
                    string appid = jsonResponse.Transaction.ApplicationIdentifier;
                    string holderName = jsonResponse.Card.HolderName;
                    string terminalID = jsonResponse.TerminalID;
                    string merchantID = jsonResponse.MerchantID;
                    string acquired = jsonResponse.Acquired;
                    //DCC
                    string salesIndicator = jsonResponse.DynamicCurrencyConversion.SalesIndicator;
                    string calculationAccepted = jsonResponse.DynamicCurrencyConversion.CalculationAccepted;
                    string marginRate = jsonResponse.DynamicCurrencyConversion.MarginRate;
                    string amountdcc = jsonResponse.DynamicCurrencyConversion.Amount;
                    string displayrate = jsonResponse.DynamicCurrencyConversion.DisplayRate;
                    string transactioncurr = jsonResponse.DynamicCurrencyConversion.TransactionCurrency;

                    SaveTransactionResult(transactionId, status, product, cardNumber, lote, referencia, authorizationNumber, mode, rrn, fechahora, appid, holderName, terminalID, merchantID, acquired, response, salesIndicator, calculationAccepted, marginRate, amountdcc, displayrate, transactioncurr);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed response: {e.Message}");
                    SaveTransactionResult(transactionId, "Failed", "", "000", "000", "000", "000", "", "", "", "", "", "", "", "000", response, "", "", "", "", "", "");
                }
            }
            else
            {
                Console.WriteLine($"Fallo de conexion con el POS:{IpRemote}");
                SaveTransactionResult(transactionId, "Failed", "", "000", "000", "000", "000", "", "", "", "", "", "", "", "000", initialiceResult, "", "", "", "", "", "");
            }
        }


        private static void LogCloseResponse(string ipRemota, string closeResponse, int idtrn)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("GuardaCierresLote", conn))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IpRemota", ipRemota);
                    cmd.Parameters.AddWithValue("@CloseResponse", closeResponse);
                    //cmd.Parameters.AddWithValue("@Host", DateTime.Now);
                    //cmd.Parameters.AddWithValue("@Batch", DateTime.Now);
                    //cmd.Parameters.AddWithValue("@fechahora", DateTime.Now);
                    //cmd.Parameters.AddWithValue("@Quantity", DateTime.Now);
                    //cmd.Parameters.AddWithValue("@Amount", DateTime.Now);
                    //cmd.Parameters.AddWithValue("@Tax", DateTime.Now);
                    //cmd.Parameters.AddWithValue("@OtherTax", DateTime.Now);
                    //cmd.Parameters.AddWithValue("@Cancel_Quantity", DateTime.Now);
                    //cmd.Parameters.AddWithValue("@Cancel_Amount", DateTime.Now);
                    //cmd.Parameters.AddWithValue("@Cancel_Tax", DateTime.Now);
                    cmd.Parameters.AddWithValue("@IDComunicacion", idtrn);

                    cmd.ExecuteNonQuery();
                }
            }
        }
    }

    class SalesTransaction
    {
        public int TransactionId { get; set; }
        public int Amount { get; set; }
        public int Discount { get; set; }
        public int Tax { get; set; }
        public int ItemCode { get; set; }
    }
}

using ECRti.Framework; // Ensure this namespace is correct and exists in the DLL
using System;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace EasyPOS_Cardnet
{
    // Pasarela de consola entre las colas de EasyPOS en SQL Server y los procesadores Cardnet o Azul.
    class Program
    {
        private static string IpLocal = "192.168.10.50";
        private const int PortNumberLocal = 2018;
        private const int PortNumberRemote = 7060;
        private const int Timeout = 180000;

        // Azul expone localmente la WebAPI que controla el Ingenico Lane 7000; esta aplicación no accede al puerto COM.
        private const string AzulBaseUrl = "http://localhost:9000";
        private static readonly HttpClient AzulHttpClient = new HttpClient
        {
            BaseAddress = new Uri(AzulBaseUrl),
            // Las operaciones Azul tienen un único intento con un timeout de 45 segundos.
            Timeout = TimeSpan.FromSeconds(45)
        };
        private static bool keepRunning = true;
        private const string connectionString = "Server=192.168.10.50;Database=EasyPOS;User Id=sa;Password=1234;MultipleActiveResultSets=True;";
        
        static void Main(string[] args)
        {
            // Formato obligatorio: EasyPOS_Cardnet <destino> <operacion> <proveedor>.
            // Cada proceso atiende exclusivamente Cardnet o Azul, sin proveedor predeterminado.
            if (args.Length != 3 ||
                (!args[2].Equals("Cardnet", StringComparison.OrdinalIgnoreCase) &&
                 !args[2].Equals("Azul", StringComparison.OrdinalIgnoreCase)))
            {
                Console.Error.WriteLine("Formato correcto: EasyPOS_Cardnet <destino> <operacion> <proveedor>");
                Console.Error.WriteLine("Valores permitidos para <proveedor>: Cardnet, Azul");
                Environment.ExitCode = 1;
                return;
            }

            string IpRemote = args[0];
            string switch_on = args[1];
            string proveedor = args[2].Equals("Cardnet", StringComparison.OrdinalIgnoreCase) ? "Cardnet" : "Azul";
            IpLocal = Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? "No IPv4 address found.";
                    

            //Console.WriteLine(Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork));



            Console.WriteLine($"Recibiendo transacciones del POS{IpLocal} con destino: {IpRemote}, operacion: {switch_on} y proveedor: {proveedor}");

            // Handle graceful shutdown
            Console.CancelKeyPress += (sender, e) => OnExit();
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => OnExit();

           


            // Ventas permanece consultando la cola hasta recibir Ctrl+C. Las rutas Azul de
            // cancelaciones y cierres terminan después de procesar las filas recuperadas.
            // Una consulta puede devolver más de una fila: la ejecución procesa todas las
            // operaciones pendientes que coincidan con el destino indicado.
            while (keepRunning)
            {
                try
                {
                    switch (switch_on)
                    {
                        case "Cierres":
                            if (proveedor == "Azul")
                            {
                                ProcessAzulClosures(IpRemote);
                                Environment.Exit(0);
                                break;
                            }
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
                            if (proveedor == "Azul")
                            {
                                ProcessAzulSalesTransactionsSQL(IpRemote);
                            }
                            else
                            {
                                ProcessSalesTransactionsSQLVer1(IpRemote);
                            }
                            break;
                        case "Cancelaciones":
                            if (proveedor == "Azul")
                            {
                                ProcessAzulCancelations(IpRemote);
                                Environment.Exit(0);
                                break;
                            }
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

        static void ProcessAzulSalesTransactionsSQL(string destino)
        {
            // Reutiliza la cola SQL de ventas y procesa todas las filas devueltas.
            // C200 (consulta) y C300 (cuotas) no se convierten en ventas Azul.
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string storedProcedure = destino == "All" ? "Procesar_POS_All" : "Procesar_POS";
                    using (SqlCommand command = new SqlCommand(storedProcedure, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        if (destino != "All")
                        {
                            command.Parameters.AddWithValue("@VERIFON", destino);
                        }

                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int transactionId = reader["IDComunicacion"] != DBNull.Value ? (int)reader["IDComunicacion"] : 0;
                                decimal amount = reader["Monto"] != DBNull.Value
                                    ? Convert.ToDecimal(reader["Monto"], CultureInfo.InvariantCulture)
                                    : 0m;
                                string transactionType = reader["Transaccion"] as string ?? string.Empty;

                                if (transactionType == "C200" || transactionType == "C300")
                                {
                                    SaveAzulUnsupportedTransaction(transactionId, transactionType);
                                    continue;
                                }

                                ProcessAzulSale(transactionId, amount);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener ventas para Azul: {ex.Message}");
            }
        }

        static void ProcessAzulSale(int transactionId, decimal amount)
        {
            // Procesar_POS entrega el importe en unidades menores; la WebAPI de Azul requiere unidades monetarias.
            decimal azulAmount = amount / 100m;
            string formattedAmount = azulAmount.ToString("0.00", CultureInfo.InvariantCulture);
            AzulHttpResult result = SendAzulRequest($"/api/transaction/lane/sale/{formattedAmount}");

            if (!result.IsValidJson)
            {
                Console.WriteLine($"Venta Azul {transactionId} no completada: {result.Error}");
                SaveAzulSaleResult(transactionId, "99", null, result.StoredResponse);
                return;
            }

            string overallStatus = GetAzulOverallStatus(result.Json, transactionId.ToString(CultureInfo.InvariantCulture));
            LogAzulOperationStatus("Venta", transactionId.ToString(CultureInfo.InvariantCulture), overallStatus);
            SaveAzulSaleResult(transactionId, overallStatus, result.Json, result.Body);
        }

        static void ProcessAzulCancelations(string destino)
        {
            // ReferenceNumber permanece como int por compatibilidad con Cardnet y puede perder ceros iniciales.
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                using (SqlCommand command = new SqlCommand("dbo.Get_Cancelaciones", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@VERIFON", destino);
                    connection.Open();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = (int)reader["Id"];
                            int referenceNumber = (int)reader["ReferenceNumber"];
                            ProcessAzulVoid(id, referenceNumber);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener anulaciones para Azul: {ex.Message}");
            }
        }

        static void ProcessAzulVoid(int transactionId, int referenceNumber)
        {
            // La conversión a texto se limita al segmento InvoiceNumber de la ruta de Azul.
            string invoiceNumber = referenceNumber.ToString(CultureInfo.InvariantCulture);
            string escapedInvoiceNumber = Uri.EscapeDataString(invoiceNumber);
            AzulHttpResult result = SendAzulRequest($"/api/transaction/lane/Void/{escapedInvoiceNumber}");
            if (!result.IsValidJson)
            {
                Console.WriteLine($"Anulacion Azul {transactionId} no completada: {result.Error}");
                SaveCancelationResults(transactionId, result.StoredResponse);
                return;
            }

            string overallStatus = GetAzulOverallStatus(result.Json, transactionId.ToString(CultureInfo.InvariantCulture));
            LogAzulOperationStatus("Anulacion", transactionId.ToString(CultureInfo.InvariantCulture), overallStatus);
            SaveCancelationResults(transactionId, result.Body);
        }

        static void ProcessAzulClosures(string destino)
        {
            // Los cierres pendientes se obtienen de SQL y se envían individualmente a CloseTotals.
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                using (SqlCommand command = new SqlCommand("dbo.Get_Cierres", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@VERIFON", destino);
                    connection.Open();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = (int)reader["Id"];
                            ProcessAzulCloseTotals(destino, id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener cierres para Azul: {ex.Message}");
            }
        }

        static void ProcessAzulCloseTotals(string destino, int transactionId)
        {
            AzulHttpResult result = SendAzulRequest("/api/transaction/lane/CloseTotals");
            if (!result.IsValidJson)
            {
                Console.WriteLine($"Cierre Azul {transactionId} no completado: {result.Error}");
                LogCloseResponse(destino, result.StoredResponse, transactionId);
                return;
            }

            string overallStatus = GetAzulOverallStatus(result.Json, transactionId.ToString(CultureInfo.InvariantCulture));
            LogAzulOperationStatus("Cierre", transactionId.ToString(CultureInfo.InvariantCulture), overallStatus);
            LogCloseResponse(destino, result.Body, transactionId);
        }

        static AzulHttpResult SendAzulRequest(string route)
        {
            // Todas las operaciones Azul usan HTTP GET contra el servicio local, sin reintentos automáticos.
            // Un rechazo comercial llega como JSON válido y se interpreta fuera de este método;
            // solo transporte, HTTP, timeout o JSON inválido producen un resultado técnico fallido.
            try
            {
                using (HttpResponseMessage response = AzulHttpClient.GetAsync(route).GetAwaiter().GetResult())
                {
                    string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        string error = $"Respuesta HTTP invalida ({(int)response.StatusCode} {response.ReasonPhrase})";
                        return AzulHttpResult.Failure(body, error);
                    }

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        return AzulHttpResult.Failure(body, "La WebAPI de Azul devolvio una respuesta vacia");
                    }

                    try
                    {
                        return AzulHttpResult.Success(body, JObject.Parse(body));
                    }
                    catch (JsonException ex)
                    {
                        return AzulHttpResult.Failure(body, $"JSON invalido: {ex.Message}");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return AzulHttpResult.Failure(string.Empty, "Timeout de 45 segundos al comunicarse con Azul");
            }
            catch (HttpRequestException ex)
            {
                return AzulHttpResult.Failure(string.Empty, $"Servicio Azul no disponible: {ex.Message}");
            }
            catch (Exception ex)
            {
                return AzulHttpResult.Failure(string.Empty, $"Error de comunicacion con Azul: {ex.Message}");
            }
        }

        static void SaveAzulSaleResult(int transactionId, string status, JObject response, string rawResponse)
        {
            SaveTransactionResultVer1(
                transactionId,
                status ?? string.Empty,
                string.Empty,
                GetAzulValue(response, "MaskedPAN"),
                GetAzulValue(response, "BatchNumber"),
                GetAzulTransactionReferenceForSql(response),
                GetAzulValue(response, "HostAuthorizationCode"),
                GetAzulValue(response, "EntryMode"),
                string.Empty,
                CombineAzulDateAndTime(response),
                GetAzulValue(response, "AID"),
                GetAzulValue(response, "CardHolderName"),
                GetAzulValue(response, "TerminalId"),
                GetAzulValue(response, "MerchantId"),
                string.Empty,
                rawResponse ?? string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                GetAzulValue(response, "Currency"));
        }

        static void SaveAzulUnsupportedTransaction(int transactionId, string transactionType)
        {
            string message = $"Tipo de transaccion Azul no soportado: {transactionType}";
            Console.WriteLine($"TransactionId: {transactionId}. {message}");
            SaveAzulSaleResult(transactionId, "99", null, message);
        }

        static string CombineAzulDateAndTime(JObject response)
        {
            string date = GetAzulValue(response, "Date");
            string time = GetAzulValue(response, "Time");

            if (string.IsNullOrEmpty(date))
            {
                return time;
            }

            if (string.IsNullOrEmpty(time))
            {
                return date;
            }

            return $"{date} {time}";
        }

        static string GetAzulTransactionReferenceForSql(JObject response)
        {
            string transactionReference = GetAzulValue(response, "TransactionReference");
            if (transactionReference.Length <= 4)
            {
                return transactionReference;
            }

            // SQL admite cuatro caracteres: la referencia larga queda solo en la respuesta completa de @Trama_Recibida.
            Console.WriteLine("TransactionReference de Azul excede la longitud SQL disponible; se guardara vacio en @Reference.");
            return string.Empty;
        }

        static string GetAzulValue(JObject response, params string[] candidateNames)
        {
            if (response == null)
            {
                return string.Empty;
            }

            JProperty property = response
                .DescendantsAndSelf()
                .OfType<JProperty>()
                .FirstOrDefault(item => candidateNames.Any(name => item.Name.Equals(name, StringComparison.Ordinal)));

            return property?.Value?.Type == JTokenType.Null ? string.Empty : property?.Value?.ToString() ?? string.Empty;
        }

        static string GetAzulOverallStatus(JObject response, string identifier)
        {
            string overallStatus = GetAzulValue(response, "TransactionOverallStatus");
            if (!string.IsNullOrEmpty(overallStatus))
            {
                return overallStatus;
            }

            Console.WriteLine($"Operacion Azul {identifier} no exitosa: Respuesta Azul sin TransactionOverallStatus");
            return "99";
        }

        static void LogAzulOperationStatus(string operation, string identifier, string overallStatus)
        {
            // Azul: 00=éxito, 01=rechazo/fallo operativo y 99=error técnico o respuesta incompleta.
            if (overallStatus == "00")
            {
                Console.WriteLine($"{operation} Azul {identifier} exitosa.");
            }
            else if (overallStatus == "01")
            {
                Console.WriteLine($"{operation} Azul {identifier} rechazada.");
            }
            else
            {
                Console.WriteLine($"{operation} Azul {identifier} no exitosa. TransactionOverallStatus: {overallStatus}");
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

    class AzulHttpResult
    {
        public string Body { get; private set; }
        public JObject Json { get; private set; }
        public string Error { get; private set; }
        public bool IsValidJson => Json != null;
        public string StoredResponse => string.IsNullOrWhiteSpace(Body) ? Error : $"{Error}. Respuesta: {Body}";

        public static AzulHttpResult Success(string body, JObject json)
        {
            return new AzulHttpResult { Body = body, Json = json, Error = string.Empty };
        }

        public static AzulHttpResult Failure(string body, string error)
        {
            return new AzulHttpResult { Body = body ?? string.Empty, Json = null, Error = error };
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

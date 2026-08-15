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
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using Serilog.Events;

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
        private static volatile bool keepRunning = true;
        private static readonly object terminalLock = new object();
        private static int salesWaiting;
        private const string AutomaticSqlDestination = "__AUTO_SQL_ROUTE__";
        private const string connectionString = "Server=192.168.10.50;Database=EasyPOS;User Id=sa;Password=1234;MultipleActiveResultSets=True;";
        
        static void Main(string[] args)
        {
            if (WindowsServiceHelpers.IsWindowsService())
            {
                if (!TryGetServiceArguments(args, out string serviceDestination, out string serviceProvider))
                {
                    Environment.ExitCode = 1;
                    return;
                }

                RunAsWindowsService(serviceDestination, serviceProvider);
                return;
            }

            if (args.Length > 0 &&
                (args[0].Equals("--service", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("--service-auto", StringComparison.OrdinalIgnoreCase)))
            {
                Console.Error.WriteLine("Los parametros de servicio solo pueden utilizarlos el Administrador de servicios de Windows.");
                Environment.ExitCode = 1;
                return;
            }

            // Formato recomendado: EasyPOS_Gateway <destino> <proveedor>.
            // El formato anterior con una operacion explicita se conserva temporalmente.
            bool unifiedMode = args.Length == 2;
            bool legacyMode = args.Length == 3;
            string providerArgument = unifiedMode ? args[1] : legacyMode ? args[2] : string.Empty;

            if ((!unifiedMode && !legacyMode) ||
                (!providerArgument.Equals("Cardnet", StringComparison.OrdinalIgnoreCase) &&
                 !providerArgument.Equals("Azul", StringComparison.OrdinalIgnoreCase)))
            {
                Console.Error.WriteLine("Formato recomendado: EasyPOS_Gateway <destino> <proveedor>");
                Console.Error.WriteLine("Formato compatible: EasyPOS_Gateway <destino> <operacion> <proveedor>");
                Console.Error.WriteLine("Valores permitidos para <proveedor>: Cardnet, Azul");
                Environment.ExitCode = 1;
                return;
            }

            string IpRemote = args[0];
            string switch_on = legacyMode ? args[1] : "Todas";
            string proveedor = providerArgument.Equals("Cardnet", StringComparison.OrdinalIgnoreCase) ? "Cardnet" : "Azul";
            IpLocal = Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? "No IPv4 address found.";
                    

            //Console.WriteLine(Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork));



            Console.WriteLine($"Recibiendo transacciones del POS {IpLocal} con destino: {IpRemote}, operacion: {switch_on} y proveedor: {proveedor}");

            // Handle graceful shutdown
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                OnExit();
            };
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => OnExit();

            if (unifiedMode)
            {
                RunUnifiedMode(IpRemote, proveedor);
                return;
            }

           


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
                        string referenceNumber = Convert.ToString(reader["ReferenceNumber"], CultureInfo.InvariantCulture) ?? string.Empty;
                        if (!int.TryParse(referenceNumber, NumberStyles.None, CultureInfo.InvariantCulture, out int referencia))
                        {
                            Console.WriteLine($"Cancelacion Cardnet {idtrn} no procesada: ReferenceNumber no es numerico.");
                            continue;
                        }

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
                    return;
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

        static void SaveTransactionResult(int transactionId, string status, string product, string cardNumber, string lote, string referencia, string authorizationNumber, string mode, string rrn, string fechahora, string appid, string holderName, string terminalid, string merchantid, string acquired, string response, string salesIndicator, string calculationAccepted, string marginRate,string  amountdcc, string displayrate,string transactioncurr, string company = "Cardnet")
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
                command.Parameters.AddWithValue("@Company", company);

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
            string transactioncurr,
            string company = "Cardnet")
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
                        command.Parameters.AddWithValue("@Company", company ?? DBNull.Value.ToString());

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

        static bool TryGetServiceArguments(string[] args, out string destination, out string provider)
        {
            destination = string.Empty;
            provider = string.Empty;

            if (args.Length == 2 &&
                args[0].Equals("--service-auto", StringComparison.OrdinalIgnoreCase) &&
                args[1].Equals("Azul", StringComparison.OrdinalIgnoreCase))
            {
                destination = AutomaticSqlDestination;
                provider = "Azul";
                return true;
            }

            if (args.Length != 3 || !args[0].Equals("--service", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!args[2].Equals("Cardnet", StringComparison.OrdinalIgnoreCase) &&
                !args[2].Equals("Azul", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(args[1]))
                return false;

            destination = args[1];
            provider = args[2].Equals("Cardnet", StringComparison.OrdinalIgnoreCase) ? "Cardnet" : "Azul";
            return true;
        }

        static string ResolveSqlRouteLocalAddress()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
            string serverName = builder.DataSource ?? string.Empty;
            int sqlPort = 1433;

            if (serverName.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
                serverName = serverName.Substring(4);

            int commaIndex = serverName.LastIndexOf(',');
            if (commaIndex >= 0)
            {
                if (int.TryParse(serverName.Substring(commaIndex + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int parsedPort))
                    sqlPort = parsedPort;
                serverName = serverName.Substring(0, commaIndex);
            }

            int instanceIndex = serverName.IndexOf('\\');
            if (instanceIndex >= 0)
                serverName = serverName.Substring(0, instanceIndex);

            IPAddress sqlAddress = Dns.GetHostAddresses(serverName)
                .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork);
            if (sqlAddress == null)
                throw new InvalidOperationException("No fue posible resolver una direccion IPv4 para SQL Server.");

            using (Socket routeSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                routeSocket.Connect(new IPEndPoint(sqlAddress, sqlPort));
                string localAddress = ((IPEndPoint)routeSocket.LocalEndPoint).Address.ToString();
                if (string.IsNullOrWhiteSpace(localAddress) || localAddress.StartsWith("127.", StringComparison.Ordinal))
                    throw new InvalidOperationException("No fue posible determinar la direccion IPv4 LAN para acceder a SQL Server.");
                return localAddress;
            }
        }

        static void RunAsWindowsService(string destination, string provider)
        {
            string logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "logs", "PaymentGateway-.log");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 60,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: 20000000)
                .CreateLogger();

            Console.SetOut(new ServiceLogWriter(LogEventLevel.Information));
            Console.SetError(new ServiceLogWriter(LogEventLevel.Error));

            try
            {
                IHost host = Host.CreateDefaultBuilder()
                    .UseWindowsService(options =>
                    {
                        options.ServiceName = "EasyPOS.PaymentGateway";
                    })
                    .ConfigureServices(services =>
                    {
                        services.Configure<HostOptions>(options =>
                        {
                            // Permite terminar limpiamente una solicitud Azul que ya este en curso.
                            options.ShutdownTimeout = TimeSpan.FromSeconds(60);
                        });
                        services.AddSingleton<IHostedService>(new PaymentGatewayWorker(destination, provider));
                    })
                    .Build();

                host.Run();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private sealed class ServiceLogWriter : System.IO.TextWriter
        {
            private readonly LogEventLevel level;
            private readonly StringBuilder buffer = new StringBuilder();
            private readonly object writerLock = new object();

            public ServiceLogWriter(LogEventLevel level)
            {
                this.level = level;
            }

            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                lock (writerLock)
                {
                    if (value == '\n')
                    {
                        FlushBuffer();
                    }
                    else if (value != '\r')
                    {
                        buffer.Append(value);
                    }
                }
            }

            public override void WriteLine(string value)
            {
                lock (writerLock)
                {
                    if (buffer.Length > 0)
                    {
                        buffer.Append(value);
                        FlushBuffer();
                    }
                    else
                    {
                        Log.Write(level, "{Message}", value ?? string.Empty);
                    }
                }
            }

            private void FlushBuffer()
            {
                if (buffer.Length == 0)
                    return;

                Log.Write(level, "{Message}", buffer.ToString());
                buffer.Clear();
            }
        }

        private sealed class PaymentGatewayWorker : BackgroundService
        {
            private readonly string destination;
            private readonly string provider;

            public PaymentGatewayWorker(string destination, string provider)
            {
                this.destination = destination;
                this.provider = provider;
            }

            protected override Task ExecuteAsync(CancellationToken stoppingToken)
            {
                IpLocal = Dns.GetHostAddresses(Dns.GetHostName())
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString()
                    ?? "No IPv4 address found.";

                stoppingToken.Register(OnExit);
                return Task.Run(() =>
                {
                    string effectiveDestination = destination;
                    if (provider == "Azul" && destination == AutomaticSqlDestination)
                    {
                        effectiveDestination = ResolveSqlRouteLocalAddress();
                        IpLocal = effectiveDestination;
                        Console.WriteLine($"Destino SQL Azul detectado automaticamente: {effectiveDestination}");
                    }

                    RunUnifiedMode(effectiveDestination, provider);
                }, stoppingToken);
            }
        }

        static void RunUnifiedMode(string destino, string proveedor)
        {
            Console.WriteLine("Modo unificado activo. Prioridad: Ventas; luego Cancelaciones y Cierres.");
            Console.WriteLine("Presione Ctrl+C para detener el servicio.");

            Task salesWorker = Task.Run(() => RunUnifiedWorker(
                "Ventas",
                1000,
                true,
                () =>
                {
                    if (proveedor == "Azul")
                        ProcessAzulSalesTransactionsSQL(destino);
                    else
                        ProcessSalesTransactionsSQLVer1(destino);
                }));

            Task cancelationsWorker = Task.Run(() => RunUnifiedWorker(
                "Cancelaciones",
                3000,
                false,
                () =>
                {
                    if (proveedor == "Azul")
                        ProcessAzulCancelations(destino);
                    else
                        ProcessTransaction_Cancelations(destino);
                }));

            Task closuresWorker = Task.Run(() => RunUnifiedWorker(
                "Cierres",
                3000,
                false,
                () =>
                {
                    if (proveedor == "Azul")
                        ProcessAzulClosures(destino);
                    else
                        ProcessTransaction_Cierres(destino);
                }));

            Task.WaitAll(salesWorker, cancelationsWorker, closuresWorker);
        }

        static void RunUnifiedWorker(string operation, int pollingIntervalMilliseconds, bool isSalesWorker, Action processPending)
        {
            while (keepRunning)
            {
                try
                {
                    if (isSalesWorker)
                    {
                        Interlocked.Exchange(ref salesWaiting, 1);
                        lock (terminalLock)
                        {
                            processPending();
                        }
                        Interlocked.Exchange(ref salesWaiting, 0);
                    }
                    else if (Volatile.Read(ref salesWaiting) == 0)
                    {
                        lock (terminalLock)
                        {
                            // Una venta que comenzo a esperar tiene prioridad sobre las otras operaciones.
                            if (Volatile.Read(ref salesWaiting) == 0)
                                processPending();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en el trabajador de {operation}: {ex.Message}");
                }
                finally
                {
                    if (isSalesWorker)
                        Interlocked.Exchange(ref salesWaiting, 0);
                }

                SleepWhileRunning(pollingIntervalMilliseconds);
            }
        }

        static void SleepWhileRunning(int milliseconds)
        {
            const int interval = 100;
            int elapsed = 0;
            while (keepRunning && elapsed < milliseconds)
            {
                Thread.Sleep(interval);
                elapsed += interval;
            }
        }

        static void ProcessAzulSalesTransactionsSQL(
            string destino
        )
        {
            try
            {
                using (
                    SqlConnection connection =
                        new SqlConnection(connectionString)
                )
                using (
                    SqlCommand command =
                        new SqlCommand(
                            "dbo.Procesar_AZUL_POS",
                            connection
                        )
                )
                {
                    command.CommandType =
                        CommandType.StoredProcedure;

                    if (
                        string.Equals(
                            destino,
                            "All",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        command.Parameters.Add(
                            "@VERIFON",
                            SqlDbType.VarChar,
                            50
                        ).Value =
                            DBNull.Value;
                    }
                    else
                    {
                        command.Parameters.Add(
                            "@VERIFON",
                            SqlDbType.VarChar,
                            50
                        ).Value =
                            destino;
                    }

                    connection.Open();

                    using (
                        SqlDataReader reader =
                            command.ExecuteReader()
                    )
                    {
                        if (!reader.Read())
                        {
                            return;
                        }

                        int transactionId =
                            reader["IDComunicacion"] != DBNull.Value
                            ? Convert.ToInt32(
                                reader["IDComunicacion"]
                            )
                            : 0;

                        decimal amount =
                            reader["Monto"] != DBNull.Value
                            ? Convert.ToDecimal(
                                reader["Monto"],
                                CultureInfo.InvariantCulture
                            )
                            : 0m;

                        string transactionType =
                            reader["Transaccion"] == DBNull.Value
                            ? string.Empty
                            : Convert.ToString(
                                reader["Transaccion"],
                                CultureInfo.InvariantCulture
                            );

                        if (transactionId <= 0)
                        {
                            Console.WriteLine(
                                "AZUL: IDComunicacion invalido."
                            );

                            return;
                        }

                        Console.WriteLine(
                            $"AZUL reclamando transaccion " +
                            $"{transactionId}, " +
                            $"tipo {transactionType}, " +
                            $"monto menor {amount}."
                        );

                        if (
                            transactionType == "C200" ||
                            transactionType == "C300"
                        )
                        {
                            SaveAzulUnsupportedTransaction(
                                transactionId,
                                transactionType
                            );

                            return;
                        }

                        ProcessAzulSale(
                            transactionId,
                            amount
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Error obteniendo transaccion AZUL: " +
                    ex.ToString()
                );
            }
        }

        static void ProcessAzulSale(
            int transactionId,
            decimal amount
        )
        {
            decimal azulAmount =
                amount / 100m;

            string formattedAmount =
                azulAmount.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture
                );

            Console.WriteLine(
                $"AZUL inicio venta. " +
                $"ID={transactionId}, " +
                $"Monto={formattedAmount}"
            );

            AzulHttpResult result =
                SendAzulRequest(
                    "/api/transaction/lane/sale/" +
                    formattedAmount
                );

            if (!result.IsValidJson)
            {
                Console.WriteLine(
                    $"AZUL resultado tecnico incierto. " +
                    $"ID={transactionId}. " +
                    $"Error={result.Error}"
                );

                bool saved =
                    TrySaveAzulSaleResult(
                        transactionId,
                        "99",
                        null,
                        result.StoredResponse
                    );

                if (!saved)
                {
                    Log.Error(
                        "CRITICAL AZUL persistence failure. " +
                        "TransactionId={TransactionId}, " +
                        "Status={Status}, Response={Response}",
                        transactionId,
                        "99",
                        result.StoredResponse
                    );
                }

                return;
            }

            string overallStatus =
                GetAzulOverallStatus(
                    result.Json,
                    transactionId.ToString(
                        CultureInfo.InvariantCulture
                    )
                );

            string sqlStatus;

            if (overallStatus == "00")
            {
                sqlStatus = "Successful";
            }
            else if (overallStatus == "01")
            {
                sqlStatus = "01";
            }
            else
            {
                sqlStatus = "99";
            }

            Console.WriteLine(
                $"AZUL respuesta recibida. " +
                $"ID={transactionId}, " +
                $"OverallStatus={overallStatus}, " +
                $"SQLStatus={sqlStatus}, " +
                $"Invoice={GetAzulValue(result.Json, "InvoiceNumber")}, " +
                $"Authorization=" +
                $"{GetAzulValue(result.Json, "HostAuthorizationCode")}"
            );

            Log.Information(
                "AZUL SALE RESPONSE " +
                "TransactionId={TransactionId} " +
                "Status={Status} " +
                "InvoiceNumber={InvoiceNumber} " +
                "Authorization={Authorization} " +
                "TransactionReference={TransactionReference} " +
                "Response={Response}",
                transactionId,
                sqlStatus,
                GetAzulValue(
                    result.Json,
                    "InvoiceNumber"
                ),
                GetAzulValue(
                    result.Json,
                    "HostAuthorizationCode"
                ),
                GetAzulValue(
                    result.Json,
                    "TransactionReference"
                ),
                result.Body
            );

            bool resultSaved =
                TrySaveAzulSaleResult(
                    transactionId,
                    sqlStatus,
                    result.Json,
                    result.Body
                );

            if (!resultSaved)
            {
                Log.Error(
                    "CRITICAL: AZUL responded but SQL " +
                    "could not persist result. " +
                    "TransactionId={TransactionId}, " +
                    "Status={Status}, " +
                    "Invoice={Invoice}, " +
                    "Authorization={Authorization}, " +
                    "Response={Response}",
                    transactionId,
                    sqlStatus,
                    GetAzulValue(
                        result.Json,
                        "InvoiceNumber"
                    ),
                    GetAzulValue(
                        result.Json,
                        "HostAuthorizationCode"
                    ),
                    result.Body
                );
            }
        }

        static void ProcessAzulCancelations(string destino)
        {
            // ReferenceNumber se conserva como texto para mantener íntegro el InvoiceNumber de Azul.
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
                            string referenceNumber = Convert.ToString(reader["ReferenceNumber"], CultureInfo.InvariantCulture) ?? string.Empty;
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

        static void ProcessAzulVoid(int transactionId, string referenceNumber)
        {
            string escapedInvoiceNumber = Uri.EscapeDataString(referenceNumber);
            AzulHttpResult result = SendAzulRequest($"/api/transaction/lane/Void/{escapedInvoiceNumber}");
            if (!result.IsValidJson)
            {
                Console.WriteLine($"Anulacion Azul {transactionId} no completada: {result.Error}");
                SaveAzulCancelationResult(transactionId, "99", null, result.StoredResponse);
                return;
            }

            string overallStatus = GetAzulOverallStatus(result.Json, transactionId.ToString(CultureInfo.InvariantCulture));
            LogAzulOperationStatus("Anulacion", transactionId.ToString(CultureInfo.InvariantCulture), overallStatus);
            SaveAzulCancelationResult(transactionId, overallStatus, result.Json, result.Body);
        }

        static void SaveAzulCancelationResult(int transactionId, string status, JObject response, string rawResponse)
        {
            decimal? amount = null;
            if (decimal.TryParse(
                GetAzulValue(response, "Amount"),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal parsedAmount))
            {
                amount = parsedAmount;
            }

            DateTime? transactionDateTime = null;
            string dateAndTime = $"{GetAzulValue(response, "Date")} {GetAzulValue(response, "Time")}";
            if (DateTime.TryParseExact(
                dateAndTime,
                "yyMMdd HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsedDateTime))
            {
                transactionDateTime = parsedDateTime;
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand("Voucher_SaveCanceledresult", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@Id", transactionId);
                command.Parameters.AddWithValue("@ResultMessage", rawResponse ?? string.Empty);
                command.Parameters.AddWithValue("@Company", "Azul");
                command.Parameters.AddWithValue("@Estatus", status == "00" ? "Successful" : status ?? "99");
                command.Parameters.AddWithValue("@TransactionReference", GetAzulValue(response, "TransactionReference"));
                command.Parameters.AddWithValue("@Amount", (object)amount ?? DBNull.Value);
                command.Parameters.AddWithValue("@Aprobacion", GetAzulValue(response, "HostAuthorizationCode"));
                command.Parameters.AddWithValue("@HostResponse", GetAzulValue(response, "HostResponse"));
                command.Parameters.AddWithValue("@TerminalResponse", GetAzulValue(response, "TerminalResponse"));
                command.Parameters.AddWithValue("@EntryMode", GetAzulValue(response, "EntryMode"));
                command.Parameters.AddWithValue("@BatchNumber", GetAzulValue(response, "BatchNumber"));
                command.Parameters.AddWithValue("@FechaHora", (object)transactionDateTime ?? DBNull.Value);
                command.Parameters.AddWithValue("@TerminalId", GetAzulValue(response, "TerminalId"));
                command.Parameters.AddWithValue("@MerchantId", GetAzulValue(response, "MerchantId"));
                command.Parameters.AddWithValue("@Product", GetAzulValue(response, "RangeName"));
                connection.Open();
                command.ExecuteNonQuery();
            }
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
            LogAzulCloseResponse(destino, transactionId, overallStatus, result.Json, result.Body);
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

        static bool TrySaveAzulSaleResult(
            int transactionId,
            string status,
            JObject response,
            string rawResponse
        )
        {
            const int maxAttempts = 3;

            for (
                int attempt = 1;
                attempt <= maxAttempts;
                attempt++
            )
            {
                try
                {
                    using (
                        SqlConnection connection =
                            new SqlConnection(connectionString)
                    )
                    using (
                        SqlCommand command =
                            new SqlCommand(
                                "dbo.Procesa_POS_Res",
                                connection
                            )
                    )
                    {
                        command.CommandType =
                            CommandType.StoredProcedure;

                        command.Parameters.AddWithValue(
                            "@ID_Transaction",
                            transactionId
                        );

                        command.Parameters.AddWithValue(
                            "@Aprobacion",
                            GetAzulValue(
                                response,
                                "HostAuthorizationCode"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@Estatus",
                            status ?? string.Empty
                        );

                        command.Parameters.AddWithValue(
                            "@Product",
                            GetAzulValue(
                                response,
                                "RangeName"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@CardNumber",
                            GetAzulValue(
                                response,
                                "MaskedPAN"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@Lote",
                            GetAzulValue(
                                response,
                                "BatchNumber"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@Reference",
                            GetAzulValue(
                                response,
                                "InvoiceNumber"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@Mode",
                            GetAzulValue(
                                response,
                                "EntryMode"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@rnn",
                            GetAzulValue(
                                response,
                                "TransactionReference"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@fechahora",
                            CombineAzulDateAndTime(
                                response
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@appid",
                            GetAzulValue(
                                response,
                                "AID"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@holdername",
                            GetAzulValue(
                                response,
                                "CardHolderName"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@terminalid",
                            GetAzulValue(
                                response,
                                "TerminalId"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@merchantid",
                            GetAzulValue(
                                response,
                                "MerchantId"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@acquired",
                            string.Empty
                        );

                        command.Parameters.AddWithValue(
                            "@salesIndicator",
                            GetAzulValue(
                                response,
                                "DccIndicator"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@calculationAccepted",
                            string.Empty
                        );

                        command.Parameters.AddWithValue(
                            "@marginRate",
                            GetAzulValue(
                                response,
                                "DccMargin"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@amountdcc",
                            GetAzulValue(
                                response,
                                "DccOriginalAmount"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@displayrate",
                            GetAzulValue(
                                response,
                                "DccRate"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@transactioncurr",
                            GetAzulValue(
                                response,
                                "Currency"
                            )
                        );

                        command.Parameters.AddWithValue(
                            "@Trama_Recibida",
                            rawResponse ?? string.Empty
                        );

                        command.Parameters.AddWithValue(
                            "@Company",
                            "Azul"
                        );

                        connection.Open();

                        command.ExecuteNonQuery();

                        Console.WriteLine(
                            $"AZUL resultado guardado. " +
                            $"ID={transactionId}, " +
                            $"Status={status}, " +
                            $"Intento={attempt}"
                        );

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Error guardando respuesta AZUL. " +
                        $"ID={transactionId}, " +
                        $"Intento={attempt}/{maxAttempts}. " +
                        ex.Message
                    );

                    Log.Error(
                        ex,
                        "Error persisting AZUL result. " +
                        "TransactionId={TransactionId}, " +
                        "Attempt={Attempt}, " +
                        "Status={Status}",
                        transactionId,
                        attempt,
                        status
                    );

                    if (attempt < maxAttempts)
                    {
                        Thread.Sleep(
                            attempt * 1000
                        );
                    }
                }
            }

            return false;
        }
        static void SaveAzulUnsupportedTransaction(int transactionId, string transactionType)
        {
            string message = $"Tipo de transaccion Azul no soportado: {transactionType}";
            Console.WriteLine($"TransactionId: {transactionId}. {message}");
            TrySaveAzulSaleResult(transactionId, "99", null, message);
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

        private static void LogAzulCloseResponse(string ipRemota, int idtrn, string status, JObject response, string rawResponse)
        {
            string receipt = GetAzulValue(response, "Receipts");
            int salesQuantity = 0;
            decimal salesAmount = 0m;

            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                receipt,
                @"(?m)^\s*(?:MASTERCARD|VISA|AMEX|DISCOVER)\s+(\d+)\s+([\d,]+\.\d{2})\s*$"))
            {
                salesQuantity += int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                salesAmount += decimal.Parse(match.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture);
            }

            System.Text.RegularExpressions.Match annulmentMatch = System.Text.RegularExpressions.Regex.Match(
                receipt,
                @"(?m)^\s*ANULACIONES\s+(\d+)\s+([\d,]+\.\d{2})\s*$");
            int cancelQuantity = annulmentMatch.Success
                ? int.Parse(annulmentMatch.Groups[1].Value, CultureInfo.InvariantCulture)
                : 0;
            decimal cancelAmount = annulmentMatch.Success
                ? decimal.Parse(annulmentMatch.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture)
                : 0m;

            System.Text.RegularExpressions.Match terminalMatch = System.Text.RegularExpressions.Regex.Match(
                receipt,
                @"(?m)^\s*TERMINAL ID:\s*(\S+)\s*$");
            string terminalId = terminalMatch.Success ? terminalMatch.Groups[1].Value : string.Empty;

            DateTime? transactionDateTime = null;
            string dateAndTime = $"{GetAzulValue(response, "Date")} {GetAzulValue(response, "Time")}";
            if (DateTime.TryParseExact(
                dateAndTime,
                "yyMMdd HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsedDateTime))
            {
                transactionDateTime = parsedDateTime;
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand("GuardaCierresLote", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@IpRemota", ipRemota);
                command.Parameters.AddWithValue("@CloseResponse", rawResponse ?? string.Empty);
                command.Parameters.AddWithValue("@IDComunicacion", idtrn);
                command.Parameters.AddWithValue("@Estatus", status ?? string.Empty);
                command.Parameters.AddWithValue("@TerminalId", terminalId);
                command.Parameters.AddWithValue("@FechaHora", (object)transactionDateTime ?? DBNull.Value);
                command.Parameters.AddWithValue("@Quantity", salesQuantity);
                command.Parameters.AddWithValue("@Amount", salesAmount);
                command.Parameters.AddWithValue("@CancelQuantity", cancelQuantity);
                command.Parameters.AddWithValue("@CancelAmount", cancelAmount);
                connection.Open();
                command.ExecuteNonQuery();
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

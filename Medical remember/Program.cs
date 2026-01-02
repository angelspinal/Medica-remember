using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MedicalRemember
{

    /// Representa un paciente con datos básicos.
    public struct Paciente
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public int Edad { get; set; }
        public string? CondicionMedica { get; set; }
        public string[] Alergias { get; set; }

        public Paciente(string nombre, string apellido, int edad, string? condicionMedica = null, string[]? alergias = null)
        {
            Id = Guid.NewGuid();
            Nombre = nombre;
            Apellido = apellido;
            Edad = edad;
            CondicionMedica = condicionMedica;
            Alergias = alergias ?? Array.Empty<string>();
        }

        public string NombreCompleto() => $"{Nombre} {Apellido}";
    }

    /// Representa un medicamento asociado a un paciente (referencia por Id)
    public struct Medicamento
    {
        public Guid Id { get; set; }
        public Guid PacienteId { get; set; }
        public string Nombre { get; set; }
        public string Dosis { get; set; }
        public int FrecuenciaHoras { get; set; }
        public decimal Valor { get; set; }

        public Medicamento(Guid pacienteId, string nombre, string dosis, int frecuenciaHoras, decimal valor)
        {
            Id = Guid.NewGuid();
            PacienteId = pacienteId;
            Nombre = nombre;
            Dosis = dosis;
            FrecuenciaHoras = frecuenciaHoras;
            Valor = valor;
        }
    }

    /// Registro de toma de medicamento (historial)
    public struct RegistroToma
    {
        public Guid Id { get; set; }
        public Guid PacienteId { get; set; }
        public Guid MedicamentoId { get; set; }
        public DateTime FechaHora { get; set; }
        public bool Tomado { get; set; }

        public RegistroToma(Guid pacienteId, Guid medicamentoId, DateTime fechaHora, bool tomado)
        {
            Id = Guid.NewGuid();
            PacienteId = pacienteId;
            MedicamentoId = medicamentoId;
            FechaHora = fechaHora;
            Tomado = tomado;
        }
    }

    /// Recordatorio opcionalmente asociado a un medicamento.
    public struct Recordatorio
    {
        public Guid Id { get; set; }
        public Guid PacienteId { get; set; }
        public string Mensaje { get; set; }
        public DateTime FechaHora { get; set; }
        public Guid? MedicamentoId { get; set; }
        public bool Activo { get; set; }

        public Recordatorio(Guid pacienteId, string mensaje, DateTime fechaHora, Guid? medicamentoId = null)
        {
            Id = Guid.NewGuid();
            PacienteId = pacienteId;
            Mensaje = mensaje;
            FechaHora = fechaHora;
            MedicamentoId = medicamentoId;
            Activo = true;
        }

        /// <summary>Texto legible del recordatorio; indica si está asociado a medicamento.
        public override string ToString() =>
            $"{FechaHora:dd/MM/yyyy HH:mm} - {Mensaje}{(MedicamentoId.HasValue ? " (med)" : "")} {(Activo ? "(Activo)" : "(Inactivo)")}";
        public string ToShortString() => $"{FechaHora:HH:mm} - {Mensaje}";
    }

    /// <summary>Representa credenciales de doctor (hash + salt).</summary>
    public struct Doctor
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; }
        public string Usuario { get; set; }
        public string PasswordHash { get; set; }
        public string Salt { get; set; }

        public Doctor(Guid id, string nombre, string usuario, string passwordHash, string salt)
        {
            Id = id;
            Nombre = nombre;
            Usuario = usuario;
            PasswordHash = passwordHash;
            Salt = salt;
        }

        public static Doctor Create(string nombre, string usuario, string passwordPlain)
        {
            var (hash, salt) = SecurityHelper.HashPassword(passwordPlain);
            return new Doctor(Guid.NewGuid(), nombre, usuario, hash, salt);
        }
    }

    // =====================================================================================
    // SEGURIDAD: hashing de contraseñas (PBKDF2)
    // =====================================================================================

    /// Utilidades de seguridad para hashing/verificación de contraseñas.
    static class SecurityHelper
    {
        private const int Iterations = 100_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static (string hashBase64, string saltBase64) HashPassword(string password)
        {
            var salt = new byte[SaltSize];
            RandomNumberGenerator.Fill(salt);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(HashSize);
            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        public static bool VerifyPassword(string password, string storedHashBase64, string storedSaltBase64)
        {
            try
            {
                var salt = Convert.FromBase64String(storedSaltBase64);
                var storedHash = Convert.FromBase64String(storedHashBase64);
                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
                var computed = pbkdf2.GetBytes(HashSize);
                return CryptographicOperations.FixedTimeEquals(computed, storedHash);
            }
            catch
            {
                return false;
            }
        }
    }

    // =====================================================================================
    // APLICACIÓN: lógica, UI de consola y módulos — todo organizado en funciones
    // =====================================================================================

    class Program
    {
        // -----------------------
        // Estado global (colecciones en memoria)
        // -----------------------
        static List<Paciente> pacientes = new();
        static List<Medicamento> medicamentos = new();
        static List<Recordatorio> recordatorios = new();
        static List<RegistroToma> historialTomas = new();
        static List<Doctor> doctores = new();

        // Grafo pacienteIndex -> lista de indices de medicamentos
        static Dictionary<int, List<int>> grafoPacienteMedicamentos = new();

        // Colores UI
        static readonly ConsoleColor ColorMenu = ConsoleColor.Cyan;
        static readonly ConsoleColor ColorSuccess = ConsoleColor.Green;
        static readonly ConsoleColor ColorError = ConsoleColor.Red;
        static readonly ConsoleColor ColorAccent = ConsoleColor.Yellow;

        // -----------------------
        // Punto de entrada
        // -----------------------

        /// Punto de entrada de la aplicación.
        static void Main()
        {
            InicializarDatosDePrueba();
            while (true)
            {
                Console.Clear();
                DrawHeader("SISTEMA DE RECORDATORIO MÉDICO");
                Console.WriteLine("1. Paciente");
                Console.WriteLine("2. Doctor");
                Console.WriteLine("3. Salir");
                Console.Write("\nSeleccione una opción: ");
                var opcion = Console.ReadLine() ?? string.Empty;

                switch (opcion)
                {
                    case "1": FlujoPaciente(); break;
                    case "2": LoginDoctor(); break;
                    case "3": WriteAccent("\nGracias por usar el sistema. Adiós.\n"); return;
                    default:
                        WriteError("Opción no válida. Presione una tecla...");
                        Console.ReadKey();
                        break;
                }
            }
        }

        // =================================================================================
        // UI helpers / Validaciones
        // =================================================================================

        static void DrawHeader(string title)
        {
            Console.ForegroundColor = ColorMenu;
            string line = new string('═', Math.Max(40, title.Length + 10));
            Console.WriteLine(line);
            int leftPadding = (line.Length - title.Length) / 2;
            Console.WriteLine(title.PadLeft(leftPadding + title.Length).PadRight(line.Length));
            Console.WriteLine(line);
            Console.ResetColor();
            Console.WriteLine();
        }

        static void WriteAccent(string text)
        {
            Console.ForegroundColor = ColorAccent;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        static void WriteError(string text)
        {
            Console.ForegroundColor = ColorError;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        /// Lee una cadena no vacía del usuario (validación).
        static string ReadRequired(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                var input = Console.ReadLine() ?? string.Empty;
                var trimmed = input.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    WriteError("Entrada vacía. Por favor ingrese un valor válido.");
                    continue;
                }
                return trimmed;
            }
        }

        /// Lee una contraseña ocultando la entrada (muestra '*' por cada carácter).
        static string ReadPassword(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                var sb = new StringBuilder();
                ConsoleKeyInfo keyInfo;
                while ((keyInfo = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
                {
                    if (keyInfo.Key == ConsoleKey.Backspace)
                    {
                        if (sb.Length > 0)
                        {
                            sb.Length--;
                            // Mover cursor atrás, sobrescribir con espacio y volver atrás
                            Console.Write("\b \b");
                        }
                    }
                    else if (!char.IsControl(keyInfo.KeyChar))
                    {
                        sb.Append(keyInfo.KeyChar);
                        Console.Write('*');
                    }
                }
                Console.WriteLine();
                var result = sb.ToString().Trim();
                if (string.IsNullOrEmpty(result))
                {
                    WriteError("Entrada vacía. Por favor ingrese un valor válido.");
                    continue;
                }
                return result;
            }
        }

        /// Lee un entero en rango con validación y mensaje de error claro.
        static int ReadIntInRange(string prompt, int min, int max)
        {
            while (true)
            {
                Console.Write(prompt);
                var txt = Console.ReadLine() ?? string.Empty;
                if (!int.TryParse(txt, out int val))
                {
                    WriteError("Entrada no numérica. Ingrese un número válido.");
                    continue;
                }
                if (val < min || val > max)
                {
                    WriteError($"Número fuera de rango. Ingrese un valor entre {min} y {max}.");
                    continue;
                }
                return val;
            }
        }

        /// Lee un decimal no negativo con validaciones.
        static decimal ReadDecimalNonNegative(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                var txt = Console.ReadLine() ?? string.Empty;
                if (!decimal.TryParse(txt, out decimal val))
                {
                    WriteError("Entrada no válida. Ingrese un número decimal válido.");
                    continue;
                }
                if (val < 0)
                {
                    WriteError("El valor no puede ser negativo.");
                    continue;
                }
                return val;
            }
        }

        static bool Confirm(string prompt)
        {
            Console.Write($"{prompt} (s/n): ");
            var r = (Console.ReadLine() ?? string.Empty).Trim().ToLower();
            return r == "s" || r == "si";
        }

        // =================================================================================
        // Inicialización de datos de ejemplo y construcción del grafo
        // =================================================================================

        /// Inicializa datos de ejemplo y construye el grafo paciente->medicamentos.
        static void InicializarDatosDePrueba()
        {
            // Doctor de ejemplo (usuario: dresp / contraseña: 1234)
            doctores.Add(Doctor.Create("Dr. Espinoza", "dresp", "1234"));

            var p1 = new Paciente("German", "Pérez", 35, null, new[] { "Penicilina" });
            var p2 = new Paciente("German", "López", 40, null, Array.Empty<string>());
            var p3 = new Paciente("Ana", "Gómez", 28, null, new[] { "Ácido acetilsalicílico" });
            pacientes.AddRange(new[] { p1, p2, p3 });

            var med1 = new Medicamento(p1.Id, "Paracetamol", "500mg", 8, 50m);
            var med2 = new Medicamento(p2.Id, "Ibuprofeno", "400mg", 12, 80m);
            var med3 = new Medicamento(p3.Id, "Aspirina", "100mg", 24, 25m);
            medicamentos.AddRange(new[] { med1, med2, med3 });

            // Construir grafo (índices basados en listas actuales)
            grafoPacienteMedicamentos.Clear();
            for (int i = 0; i < medicamentos.Count; i++)
            {
                var med = medicamentos[i];
                int pIndex = pacientes.FindIndex(p => p.Id == med.PacienteId);
                if (pIndex >= 0)
                {
                    if (!grafoPacienteMedicamentos.TryGetValue(pIndex, out var list))
                    {
                        list = new List<int>();
                        grafoPacienteMedicamentos[pIndex] = list;
                    }
                    if (!list.Contains(i)) list.Add(i);
                }
            }

            // Historial y recordatorios de ejemplo
            historialTomas.Add(new RegistroToma(p1.Id, med1.Id, DateTime.Now.AddHours(-7), true));
            historialTomas.Add(new RegistroToma(p2.Id, med2.Id, DateTime.Now.AddHours(-10), true));

            recordatorios.Add(new Recordatorio(p1.Id, "Tomar Paracetamol", DateTime.Now.AddHours(1), med1.Id));
        }

        // =================================================================================
        // FLUJO PACIENTE (interacción simplificada)
        // =================================================================================

        static void FlujoPaciente()
        {
            Console.Clear();
            DrawHeader("ACCESO PACIENTE");
            Console.Write("Ingrese su nombre: ");
            string nombre = Console.ReadLine() ?? string.Empty;

            var indices = pacientes.Select((p, i) => new { p, i })
                                  .Where(x => x.p.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase))
                                  .Select(x => x.i).ToList();
            if (!indices.Any())
            {
                WriteError("No se encontraron pacientes con ese nombre.");
                Console.ReadKey(); return;
            }

            Console.WriteLine("\nSeleccione su apellido:");
            for (int j = 0; j < indices.Count; j++)
            {
                var p = pacientes[indices[j]];
                Console.WriteLine($"{j + 1}. {p.NombreCompleto()}");
            }
            int sel = ReadIntInRange("Número: ", 1, indices.Count) - 1;
            MostrarMenuPaciente(indices[sel]);
        }

        static void MostrarMenuPaciente(int pacienteIndex)
        {
            while (true)
            {
                Console.Clear();
                var paciente = pacientes[pacienteIndex];
                DrawHeader($"PACIENTE: {paciente.NombreCompleto()}");
                Console.WriteLine($"Edad: {paciente.Edad}    |    Condición: {paciente.CondicionMedica}\n");

                var meds = medicamentos.Where(m => m.PacienteId == paciente.Id).ToList();
                if (!meds.Any()) WriteAccent("No tienes medicamentos registrados.\n");
                else
                {
                    Console.WriteLine("Medicamentos:");
                    for (int i = 0; i < meds.Count; i++)
                    {
                        var med = meds[i];
                        var ultima = historialTomas.Where(h => h.MedicamentoId == med.Id).OrderByDescending(h => h.FechaHora).FirstOrDefault();
                        DateTime ultimaToma = ultima.FechaHora == default ? DateTime.Now : ultima.FechaHora;
                        DateTime proxima = ultimaToma.AddHours(med.FrecuenciaHoras);
                        TimeSpan restante = proxima - DateTime.Now;

                        Console.WriteLine($"[{i + 1}] {med.Nombre} — {med.Dosis} | Valor: ${med.Valor:F2} | Cada {med.FrecuenciaHoras}h");
                        Console.WriteLine($"     Próx: {proxima:dd/MM/yyyy HH:mm}   ({(restante.TotalHours > 0 ? $"{(int)restante.TotalHours}h {restante.Minutes}m" : "¡Ahora!")})");
                    }
                    Console.WriteLine();
                }

                var recs = recordatorios.Where(r => r.PacienteId == paciente.Id && r.Activo).OrderBy(r => r.FechaHora).ToList();
                if (recs.Any())
                {
                    Console.WriteLine("Recordatorios activos:");
                    foreach (var rc in recs) Console.WriteLine($"  - {rc}");
                    Console.WriteLine();
                }

                Console.WriteLine("Opciones:");
                Console.WriteLine("1. Marcar toma como tomada ahora");
                Console.WriteLine("2. Ver historial de un medicamento");
                Console.WriteLine("3. Ver/Administrar recordatorios");
                Console.WriteLine("4. Volver");
                Console.Write("\nSeleccione: ");
                var op = Console.ReadLine() ?? string.Empty;
                switch (op)
                {
                    case "1": MarcarTomaAhora(pacienteIndex); break;
                    case "2": VerHistorialMedicamento(pacienteIndex); break;
                    case "3": MenuRecordatoriosPaciente(pacienteIndex); break;
                    case "4": return;
                    default:
                        WriteError("Opción inválida."); Console.ReadKey(); break;
                }
            }
        }

        // =================================================================================
        // FACHADAS Y VISTAS AUXILIARES
        // =================================================================================

        static void RegistrarPaciente() => RegistrarPacienteDoctor();
        static void RegistrarMedicamento() => RegistrarMedicamentoDoctor();
        static void MostrarPacientes() => VerPacientesYMedicamentos();
        static void BuscarPacientes() => MenuBusquedaDoctor();
        static void OrdenarPacientes() => ListarPacientesOrdenados();
        static void MostrarGrafo() => MostrarGrafoPacienteMedicamento();
        static void MostrarMatrices() => MenuMatricesDoctor();
        static void CalcularPromedioPrecio() => CalcularPromedioPrecios();
        static void BuscarMedicamento() => BuscarMedicamentoPorNombre();

        /// <summary>Muestra la lista de pacientes y los medicamentos asociados.</summary>
        static void VerPacientesYMedicamentos()
        {
            Console.Clear();
            DrawHeader("PACIENTES Y MEDICAMENTOS");
            if (!pacientes.Any())
            {
                WriteAccent("No hay pacientes.\n");
                Console.ReadKey();
                return;
            }

            for (int idx = 0; idx < pacientes.Count; idx++)
            {
                var p = pacientes[idx];
                Console.WriteLine($"[{idx + 1}] Paciente: {p.NombreCompleto()} | Edad: {p.Edad} | Condición: {p.CondicionMedica}");
                var meds = medicamentos.Where(m => m.PacienteId == p.Id).ToList();
                if (!meds.Any()) Console.WriteLine("  - Sin medicamentos.");
                else
                {
                    foreach (var m in meds)
                        Console.WriteLine($"  - {m.Nombre} | {m.Dosis} | Frec: {m.FrecuenciaHoras}h | Valor: ${m.Valor:F2}");
                }
                Console.WriteLine(new string('-', 50));
            }

            Console.WriteLine("\nPresione una tecla para volver...");
            Console.ReadKey();
        }

        /// <summary>Acceso de doctor: solicita credenciales y, si son válidas, abre el menú del doctor.</summary>
        static void LoginDoctor()
        {
            Console.Clear();
            DrawHeader("ACCESO DOCTOR");
            string usuario = ReadRequired("Usuario: ");
            string password = ReadPassword("Contraseña: ");

            var doc = doctores.FirstOrDefault(d => d.Usuario.Equals(usuario, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(doc.Usuario) || !SecurityHelper.VerifyPassword(password, doc.PasswordHash, doc.Salt))
            {
                WriteError("\nUsuario/contraseña incorrectos.");
                Console.ReadKey();
                return;
            }

            Console.ForegroundColor = ColorSuccess;
            Console.WriteLine($"\nBienvenido {doc.Nombre}");
            Console.ResetColor();
            Console.ReadKey();

            FlujoDoctor();
        }

        /// <summary>Menú principal para el doctor.</summary>
        static void FlujoDoctor()
        {
            while (true)
            {
                Console.Clear();
                DrawHeader("MENÚ DOCTOR");
                Console.WriteLine("1. Registrar nuevo paciente");
                Console.WriteLine("2. Registrar medicamento a paciente existente");
                Console.WriteLine("3. Crear recordatorio para paciente");
                Console.WriteLine("4. Ver pacientes y medicamentos");
                Console.WriteLine("5. Ver recordatorios de paciente");
                Console.WriteLine("6. Búsqueda y ordenamiento");
                Console.WriteLine("7. Ver grafo paciente → medicamento");
                Console.WriteLine("8. MATRICES");
                Console.WriteLine("9. Cerrar sesión");
                Console.Write("\nSeleccione: ");
                var op = Console.ReadLine() ?? string.Empty;
                switch (op)
                {
                    case "1": RegistrarPaciente(); break;
                    case "2": RegistrarMedicamento(); break;
                    case "3": CrearRecordatorioDoctor(); break;
                    case "4": VerPacientesYMedicamentos(); break;
                    case "5": VerRecordatoriosPorPaciente(); break;
                    case "6": MenuBusquedaDoctor(); break;
                    case "7": MostrarGrafo(); break;
                    case "8": MostrarMatrices(); break;
                    case "9": return;
                    default:
                        WriteError("Inválido."); Console.ReadKey();
                        break;
                }
            }
        }

        // =================================================================================
        // ADMIN: registro de paciente / medicamento / recordatorio
        // =================================================================================

        static void RegistrarPacienteDoctor()
        {
            Console.Clear();
            DrawHeader("REGISTRAR PACIENTE");
            string nombre = ReadRequired("Nombre: ");
            string apellido = ReadRequired("Apellido: ");

            bool dup = pacientes.Any(p => p.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase)
                                       && p.Apellido.Equals(apellido, StringComparison.OrdinalIgnoreCase));
            if (dup)
            {
                WriteError("Ya existe un paciente con el mismo nombre y apellido.");
                if (!Confirm("¿Desea continuar y crear duplicado?")) { Console.WriteLine("Operación cancelada."); Console.ReadKey(); return; }
            }

            int edad = ReadIntInRange("Edad: ", 0, 130);
            Console.Write("Condición médica (opcional): ");
            string? condicion = Console.ReadLine();

            Console.Write("Alergias (separadas por coma, opcional): ");
            string? alergiasTxt = Console.ReadLine();
            string[] alergias = string.IsNullOrWhiteSpace(alergiasTxt)
                ? Array.Empty<string>()
                : alergiasTxt.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();

            var paciente = new Paciente(nombre, apellido, edad, string.IsNullOrWhiteSpace(condicion) ? null : condicion.Trim(), alergias);

            while (pacientes.Any(p => p.Id == paciente.Id)) paciente.Id = Guid.NewGuid();

            pacientes.Add(paciente);
            Console.ForegroundColor = ColorSuccess; Console.WriteLine("\nPaciente creado."); Console.ResetColor(); Console.ReadKey();
        }

        static void RegistrarMedicamentoDoctor(int pacienteIndex)
        {
            if (pacienteIndex < 0 || pacienteIndex >= pacientes.Count) { WriteError("Índice de paciente inválido."); Console.ReadKey(); return; }
            var p = pacientes[pacienteIndex];

            string nombre = ReadRequired("Nombre medicamento: ");
            bool dupMed = medicamentos.Any(m => m.PacienteId == p.Id && m.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase));
            if (dupMed)
            {
                WriteError("El paciente ya tiene registrado un medicamento con ese nombre.");
                if (!Confirm("¿Desea continuar y crear duplicado?")) { Console.WriteLine("Operación cancelada."); Console.ReadKey(); return; }
            }

            string dosis = ReadRequired("Dosis: ");
            int freq = ReadIntInRange("Frecuencia (horas entre tomas): ", 1, 24 * 365);
            decimal valor = ReadDecimalNonNegative("Valor del medicamento: ");

            var nuevoMed = new Medicamento(p.Id, nombre, dosis, freq, valor);
            while (medicamentos.Any(m => m.Id == nuevoMed.Id)) nuevoMed.Id = Guid.NewGuid();

            medicamentos.Add(nuevoMed);

            int medIndex = medicamentos.Count - 1;
            if (!grafoPacienteMedicamentos.TryGetValue(pacienteIndex, out var list)) grafoPacienteMedicamentos[pacienteIndex] = list = new List<int>();
            if (!list.Contains(medIndex)) list.Add(medIndex);

            Console.ForegroundColor = ColorSuccess; Console.WriteLine("\nMedicamento creado y arista añadida al grafo."); Console.ResetColor(); Console.ReadKey();
        }

        static void RegistrarMedicamentoDoctor()
        {
            if (!pacientes.Any()) { WriteError("No hay pacientes."); Console.ReadKey(); return; }
            Console.WriteLine("Seleccione paciente:");
            for (int i = 0; i < pacientes.Count; i++) Console.WriteLine($"{i + 1}. {pacientes[i].NombreCompleto()}");
            int sel = ReadIntInRange("Número de paciente: ", 1, pacientes.Count) - 1;
            RegistrarMedicamentoDoctor(sel);
        }

        static void CrearRecordatorioDoctor()
        {
            if (!pacientes.Any()) { WriteError("No hay pacientes."); Console.ReadKey(); return; }
            Console.WriteLine("Seleccione paciente:");
            for (int i = 0; i < pacientes.Count; i++) Console.WriteLine($"{i + 1}. {pacientes[i].NombreCompleto()}");
            int sel = ReadIntInRange("Número de paciente: ", 1, pacientes.Count) - 1;
            var p = pacientes[sel];

            Console.Write("Asociar a medicamento? (s/n): ");
            string resp = (Console.ReadLine() ?? "").Trim().ToLower();
            Guid? medId = null;
            var meds = medicamentos.Where(m => m.PacienteId == p.Id).ToList();
            if (resp == "s" && meds.Any())
            {
                for (int i = 0; i < meds.Count; i++) Console.WriteLine($"{i + 1}. {meds[i].Nombre}");
                int msel = ReadIntInRange("Seleccione medicamento: ", 1, meds.Count) - 1;
                medId = meds[msel].Id;
            }

            string msg = ReadRequired("Mensaje: ");
            Console.Write("Fecha (dd/MM/yyyy HH:mm) o vacío para ahora: ");
            string fi = Console.ReadLine() ?? string.Empty;
            DateTime fecha = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(fi) && DateTime.TryParseExact(fi, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)) fecha = parsed;
            recordatorios.Add(new Recordatorio(p.Id, msg, fecha, medId));
            Console.ForegroundColor = ColorSuccess; Console.WriteLine("\nRecordatorio creado."); Console.ResetColor(); Console.ReadKey();
        }

        static void VerRecordatoriosPorPaciente()
        {
            if (!pacientes.Any()) { WriteError("No hay pacientes."); Console.ReadKey(); return; }
            Console.WriteLine("Seleccione paciente:");
            for (int i = 0; i < pacientes.Count; i++) Console.WriteLine($"{i + 1}. {pacientes[i].NombreCompleto()}");
            int sel = ReadIntInRange("Número de paciente: ", 1, pacientes.Count) - 1;
            var p = pacientes[sel];
            var recs = recordatorios.Where(r => r.PacienteId == p.Id).OrderBy(r => r.FechaHora).ToList();
            DrawHeader($"RECORDATORIOS — {p.NombreCompleto()}");
            if (!recs.Any()) WriteAccent("  No hay recordatorios.\n");
            else foreach (var r in recs) Console.WriteLine($"  - {r}");
            Console.ReadKey();
        }

        // =================================================================================
        // GRAFO: mostrar
        // =================================================================================

        static void MostrarGrafoPacienteMedicamento()
        {
            Console.Clear();
            DrawHeader("GRAFO PACIENTE → MEDICAMENTO");
            if (!grafoPacienteMedicamentos.Any())
            {
                WriteAccent("El grafo está vacío.\n");
                Console.ReadKey();
                return;
            }

            foreach (var kv in grafoPacienteMedicamentos.OrderBy(k => k.Key))
            {
                int pIndex = kv.Key;
                string pacienteName = (pIndex >= 0 && pIndex < pacientes.Count) ? pacientes[pIndex].NombreCompleto() : "Paciente_desconocido";
                var medsIdx = kv.Value;
                Console.WriteLine($"Paciente [{pIndex}] {pacienteName}:");
                if (medsIdx == null || medsIdx.Count == 0) Console.WriteLine("  - (sin medicamentos)");
                else
                {
                    foreach (var mIndex in medsIdx)
                    {
                        string medName = (mIndex >= 0 && mIndex < medicamentos.Count) ? medicamentos[mIndex].Nombre : "Medicamento_desconocido";
                        Console.WriteLine($"  -> Medicamento [{mIndex}] {medName}");
                    }
                }
                Console.WriteLine();
            }

            Console.WriteLine("Presione una tecla para volver...");
            Console.ReadKey();
        }

        // =================================================================================
        // MATRICES: vector y matriz bidimensional, operaciones
        // =================================================================================

        static string[] GenerarVectorNombres() => medicamentos.Select(m => m.Nombre).ToArray();

        static (decimal[,], string[,]) GenerarMatricesPrecios()
        {
            int n = medicamentos.Count;
            decimal[,] matrizNum = new decimal[n, 2];      // [precio, frecuencia]
            string[,] matrizDetalles = new string[n, 3];   // [Id, Precio, Frecuencia]
            for (int i = 0; i < n; i++)
            {
                var m = medicamentos[i];
                matrizNum[i, 0] = m.Valor;
                matrizNum[i, 1] = m.FrecuenciaHoras;
                matrizDetalles[i, 0] = m.Id.ToString();
                matrizDetalles[i, 1] = m.Valor.ToString("F2", CultureInfo.InvariantCulture);
                matrizDetalles[i, 2] = m.FrecuenciaHoras.ToString();
            }
            return (matrizNum, matrizDetalles);
        }

        static void MenuMatricesDoctor()
        {
            while (true)
            {
                Console.Clear();
                DrawHeader("MATRICES - OPERACIONES");
                Console.WriteLine("1. Ver matriz lineal de nombres");
                Console.WriteLine("2. Ver matriz de precios (ID | Precio | Frecuencia)");
                Console.WriteLine("3. Calcular promedio de precios");
                Console.WriteLine("4. Buscar medicamento (vector)");
                Console.WriteLine("5. Ver medicamento más caro");
                Console.WriteLine("6. Volver");
                Console.Write("\nSeleccione: ");
                var op = Console.ReadLine() ?? string.Empty;
                switch (op)
                {
                    case "1": MostrarVectorNombres(); break;
                    case "2": MostrarMatrizPrecios(); break;
                    case "3": CalcularPromedioPrecios(); break;
                    case "4": BuscarEnVectorMedicamentos(); break;
                    case "5": VerMedicamentoMasCaro(); break;
                    case "6": return;
                    default: WriteError("Inválido."); Console.ReadKey(); break;
                }
            }
        }

        static void MostrarVectorNombres()
        {
            Console.Clear();
            DrawHeader("VECTOR: NOMBRES DE MEDICAMENTOS");
            var nombres = GenerarVectorNombres();
            if (nombres.Length == 0) { WriteAccent("No hay medicamentos.\n"); Console.ReadKey(); return; }
            for (int i = 0; i < nombres.Length; i++) Console.WriteLine($"[{i}] {nombres[i]}");
            Console.ReadKey();
        }

        static void MostrarMatrizPrecios()
        {
            Console.Clear();
            DrawHeader("MATRIZ: ID | PRECIO | FRECUENCIA");
            var (_, detalles) = GenerarMatricesPrecios();
            int n = detalles.GetLength(0);
            if (n == 0) { WriteAccent("No hay medicamentos.\n"); Console.ReadKey(); return; }
            for (int i = 0; i < n; i++) Console.WriteLine($"[{i}] ID: {detalles[i, 0]} | Precio: ${detalles[i, 1]} | Frec: {detalles[i, 2]}h");
            Console.ReadKey();
        }

        static void CalcularPromedioPrecios()
        {
            if (!medicamentos.Any()) { WriteError("No hay medicamentos para calcular el promedio."); Console.ReadKey(); return; }
            var (matrizNum, _) = GenerarMatricesPrecios();
            int n = matrizNum.GetLength(0);
            decimal sum = 0m;
            for (int i = 0; i < n; i++) sum += matrizNum[i, 0];
            decimal promedio = sum / n;
            Console.Clear();
            DrawHeader("PROMEDIO DE PRECIOS");
            Console.WriteLine($"Promedio de precios: ${promedio:F2}");
            Console.ReadKey();
        }

        static void BuscarEnVectorMedicamentos()
        {
            Console.Clear();
            DrawHeader("BUSCAR MEDICAMENTO (VECTOR)");
            var nombres = GenerarVectorNombres();
            if (nombres.Length == 0) { WriteAccent("No hay medicamentos.\n"); Console.ReadKey(); return; }
            Console.Write("Texto a buscar (parcial, case-insensitive): ");
            string criterio = Console.ReadLine() ?? string.Empty;
            var encontrados = nombres.Select((name, idx) => new { name, idx })
                .Where(x => x.name.Contains(criterio, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!encontrados.Any()) { WriteAccent("No se encontraron coincidencias.\n"); Console.ReadKey(); return; }
            Console.WriteLine("\nResultados:");
            foreach (var e in encontrados) Console.WriteLine($"[{e.idx}] {e.name}");
            Console.Write("\nSeleccione índice para ver detalles o Enter para volver: ");
            var selTxt = Console.ReadLine();
            if (int.TryParse(selTxt, out int sel) && sel >= 0 && sel < nombres.Length)
            {
                var med = medicamentos[sel];
                Console.Clear();
                DrawHeader($"DETALLE MEDICAMENTO — {med.Nombre}");
                Console.WriteLine($"ID: {med.Id}");
                Console.WriteLine($"Dosis: {med.Dosis}");
                Console.WriteLine($"Frecuencia: {med.FrecuenciaHoras}h");
                Console.WriteLine($"Precio: ${med.Valor:F2}");
                Console.ReadKey();
            }
        }

        static void VerMedicamentoMasCaro()
        {
            Console.Clear();
            DrawHeader("MEDICAMENTO MÁS CARO");
            if (!medicamentos.Any()) { WriteAccent("No hay medicamentos.\n"); Console.ReadKey(); return; }
            decimal max = medicamentos.Max(m => m.Valor);
            var masCaros = medicamentos.Where(m => m.Valor == max).ToList();
            Console.WriteLine($"Precio máximo: ${max:F2}\n");
            foreach (var m in masCaros)
            {
                var p = pacientes.FirstOrDefault(x => x.Id == m.PacienteId);
                Console.WriteLine($"- {m.Nombre} | Dosis: {m.Dosis} | Paciente: {p.NombreCompleto()} | Frec: {m.FrecuenciaHoras}h");
            }
            Console.ReadKey();
        }

        // =================================================================================
        // BÚSQUEDA / ORDENAMIENTO / VISTAS
        // =================================================================================

        static void MenuBusquedaDoctor()
        {
            while (true)
            {
                Console.Clear();
                DrawHeader("BÚSQUEDA Y ORDENAMIENTO");
                Console.WriteLine("1. Listar pacientes en orden alfabético");
                Console.WriteLine("2. Ordenar medicamentos por frecuencia (asc)");
                Console.WriteLine("3. Buscar paciente (Find - nombre exacto)");
                Console.WriteLine("4. Buscar pacientes (Where - parcial) y ver detalles");
                Console.WriteLine("5. Buscar medicamentos por nombre (Where)");
                Console.WriteLine("6. Volver");
                Console.Write("\nSeleccione: ");
                var op = Console.ReadLine() ?? string.Empty;
                switch (op)
                {
                    case "1": ListarPacientesOrdenados(); break;
                    case "2": OrdenarMedicamentosPorFrecuencia(); break;
                    case "3": BuscarPacienteFind(); break;
                    case "4": BuscarPacientesWhere(); break;
                    case "5": BuscarMedicamentoPorNombre(); break;
                    case "6": return;
                    default: WriteError("Inválido."); Console.ReadKey(); break;
                }
            }
        }

        static void ListarPacientesOrdenados()
        {
            Console.Clear();
            var orden = pacientes.OrderBy(p => p.Nombre, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Apellido).ToList();
            DrawHeader("PACIENTES (A - Z)");
            if (!orden.Any()) WriteAccent("No hay pacientes.\n");
            else { int i = 1; foreach (var p in orden) Console.WriteLine($"{i++}. {p.NombreCompleto()} | {p.Edad} años"); }
            Console.ReadKey();
        }

        static void OrdenarMedicamentosPorFrecuencia()
        {
            Console.Clear();
            var orden = medicamentos.OrderBy(m => m.FrecuenciaHoras).ToList();
            DrawHeader("MEDICAMENTOS POR FRECUENCIA");
            if (!orden.Any()) WriteAccent("No hay medicamentos.\n");
            else
            {
                int i = 1;
                foreach (var m in orden)
                {
                    var p = pacientes.FirstOrDefault(x => x.Id == m.PacienteId);
                    Console.WriteLine($"{i++}. {m.Nombre} | {m.Dosis} | {m.FrecuenciaHoras}h | Paciente: {p.NombreCompleto()}");
                }
            }
            Console.ReadKey();
        }

        static void BuscarPacienteFind()
        {
            Console.Clear();
            DrawHeader("BUSCAR PACIENTE (EXACTO)");
            string nombre = ReadRequired("Nombre exacto: ");
            var encontrado = pacientes.Find(p => p.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(encontrado.Nombre)) { WriteError("No encontrado."); Console.ReadKey(); return; }
            MostrarDetallePaciente(encontrado.Id);
        }

        static void BuscarPacientesWhere()
        {
            Console.Clear();
            DrawHeader("BUSCAR PACIENTES (PARCIAL)");
            Console.Write("Texto a buscar en nombre: ");
            string criterio = Console.ReadLine() ?? string.Empty;
            var resultados = pacientes.Where(p => p.Nombre.Contains(criterio, StringComparison.OrdinalIgnoreCase))
                                     .OrderBy(p => p.Nombre).ThenBy(p => p.Apellido).ToList();
            Console.WriteLine("\nResultados:");
            if (!resultados.Any()) { WriteAccent("No hay coincidencias.\n"); Console.ReadKey(); return; }
            for (int i = 0; i < resultados.Count; i++)
                Console.WriteLine($"{i + 1}. {resultados[i].NombreCompleto()} | {resultados[i].Edad} años");

            Console.Write("\nSeleccione número para ver detalles o Enter para volver: ");
            var selTxt = Console.ReadLine();
            if (int.TryParse(selTxt, out int sel) && sel > 0 && sel <= resultados.Count)
            {
                MostrarDetallePaciente(resultados[sel - 1].Id);
            }
        }

        static void BuscarMedicamentoPorNombre()
        {
            Console.Clear();
            DrawHeader("BUSCAR MEDICAMENTO");
            Console.Write("Texto para buscar medicamentos: ");
            string criterio = Console.ReadLine() ?? string.Empty;
            var res = medicamentos.Where(m => m.Nombre.Contains(criterio, StringComparison.OrdinalIgnoreCase)).OrderBy(m => m.Nombre).ToList();
            if (!res.Any()) { WriteAccent("No hay coincidencias.\n"); Console.ReadKey(); return; }
            int i = 1;
            foreach (var m in res)
            {
                var p = pacientes.FirstOrDefault(x => x.Id == m.PacienteId);
                Console.WriteLine($"{i++}. {m.Nombre} | {m.Dosis} | Frec: {m.FrecuenciaHoras}h | Paciente: {p.NombreCompleto()}");
            }
            Console.ReadKey();
        }

        static void MostrarDetallePaciente(Guid pacienteId)
        {
            Console.Clear();
            var paciente = pacientes.First(p => p.Id == pacienteId);
            DrawHeader($"DETALLE PACIENTE — {paciente.NombreCompleto()}");
            Console.WriteLine($"Edad: {paciente.Edad}");
            Console.WriteLine($"Condición: {paciente.CondicionMedica}\n");

            Console.WriteLine("Alergias:");
            if (paciente.Alergias == null || paciente.Alergias.Length == 0) Console.WriteLine("  - Ninguna registrada.");
            else foreach (var a in paciente.Alergias) Console.WriteLine($"  - {a}");
            Console.WriteLine();

            var meds = medicamentos.Where(m => m.PacienteId == paciente.Id).ToList();
            Console.WriteLine("Medicamentos:");
            if (!meds.Any()) Console.WriteLine("  - Sin medicamentos.");
            else
            {
                foreach (var med in meds)
                {
                    Console.WriteLine($"  - {med.Nombre} | {med.Dosis} | Cada {med.FrecuenciaHoras}h | Valor: ${med.Valor:F2}");
                    var hist = historialTomas.Where(h => h.MedicamentoId == med.Id).OrderByDescending(h => h.FechaHora).Take(5).ToList();
                    if (hist.Any())
                    {
                        Console.WriteLine("     Últimas tomas:");
                        foreach (var h in hist) Console.WriteLine($"       • {h.FechaHora:dd/MM/yyyy HH:mm} | Tomado: {(h.Tomado ? "Sí" : "No")}");
                    }
                }
            }

            var recs = recordatorios.Where(r => r.PacienteId == paciente.Id).OrderBy(r => r.FechaHora).ToList();
            Console.WriteLine("\nRecordatorios:");
            if (!recs.Any()) Console.WriteLine("  - Sin recordatorios.");
            else foreach (var r in recs) Console.WriteLine($"  - {r}");

            Console.WriteLine("\nPresione una tecla para volver...");
            Console.ReadKey();
        }

        // =================================================================================
        // RECORDATORIOS
        // =================================================================================

        static void MenuRecordatoriosPaciente(int pacienteIndex)
        {
            while (true)
            {
                Console.Clear();
                var paciente = pacientes[pacienteIndex];
                var recs = recordatorios.Where(r => r.PacienteId == paciente.Id).ToList();
                DrawHeader($"RECORDATORIOS — {paciente.NombreCompleto()}");
                if (!recs.Any()) WriteAccent("  No hay recordatorios.\n");
                else for (int i = 0; i < recs.Count; i++) Console.WriteLine($"{i + 1}. {recs[i]}");

                Console.WriteLine("\n1. Desactivar/Activar recordatorio");
                Console.WriteLine("2. Volver");
                Console.Write("Seleccione: ");
                var op = Console.ReadLine() ?? string.Empty;
                if (op == "1")
                {
                    if (!recs.Any()) { WriteError("No hay recordatorios."); Console.ReadKey(); continue; }
                    int sel = ReadIntInRange("Número del recordatorio: ", 1, recs.Count) - 1;
                    var rec = recs[sel];
                    int idx = recordatorios.FindIndex(r => r.Id == rec.Id);
                    rec.Activo = !rec.Activo;
                    recordatorios[idx] = rec;
                    Console.ForegroundColor = ColorSuccess; Console.WriteLine("\nCambiado."); Console.ResetColor(); Console.ReadKey();
                }
                else if (op == "2") return;
                else { WriteError("Inválido."); Console.ReadKey(); }
            }
        }

        // =================================================================================
        // HISTORIAL / TOMAS
        // =================================================================================

        static void MarcarTomaAhora(int pacienteIndex)
        {
            var paciente = pacientes[pacienteIndex];
            var meds = medicamentos.Where(m => m.PacienteId == paciente.Id).ToList();
            if (!meds.Any()) { WriteError("No hay medicamentos."); Console.ReadKey(); return; }

            Console.WriteLine("\nSeleccione medicamento:");
            for (int i = 0; i < meds.Count; i++) Console.WriteLine($"{i + 1}. {meds[i].Nombre} ({meds[i].Dosis})");
            int sel = ReadIntInRange("Número: ", 1, meds.Count) - 1;

            var med = meds[sel];
            historialTomas.Add(new RegistroToma(paciente.Id, med.Id, DateTime.Now, true));
            Console.ForegroundColor = ColorSuccess; Console.WriteLine("\nToma registrada."); Console.ResetColor(); Console.ReadKey();
        }

        static void VerHistorialMedicamento(int pacienteIndex)
        {
            var paciente = pacientes[pacienteIndex];
            var meds = medicamentos.Where(m => m.PacienteId == paciente.Id).ToList();
            if (!meds.Any()) { WriteError("No hay medicamentos."); Console.ReadKey(); return; }

            Console.WriteLine("\nSeleccione medicamento:");
            for (int i = 0; i < meds.Count; i++) Console.WriteLine($"{i + 1}. {meds[i].Nombre}");
            int sel = ReadIntInRange("Número: ", 1, meds.Count) - 1;

            var med = meds[sel];
            var hist = historialTomas.Where(h => h.MedicamentoId == med.Id).OrderByDescending(h => h.FechaHora).ToList();
            DrawHeader($"HISTORIAL: {med.Nombre}");
            if (!hist.Any()) WriteAccent("  Sin registros.\n");
            else
            {
                foreach (var r in hist)
                {
                    Console.WriteLine($" - {r.FechaHora:dd/MM/yyyy HH:mm} | Tomado: {(r.Tomado ? "Sí" : "No")}");
                }
            }
            Console.WriteLine("\nPresione una tecla para volver...");
            Console.ReadKey();
        }

        // =================================================================================
        // Helpers finales
        // =================================================================================

        static void WriteSuccess(string text)
        {
            Console.ForegroundColor = ColorSuccess;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
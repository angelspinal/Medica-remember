using System;
using System.Collections.Generic;

// Clases principales
class Paciente
{
    public string Nombre { get; set; }
    public string Apellido { get; set; }
    public int Edad { get; set; }
    public string? CondicionMedica { get; set; }
    public List<Medicamento> Medicamentos { get; set; } = new();
}

class Medicamento
{
    public string Nombre { get; set; }
    public string Dosis { get; set; }
    public int FrecuenciaHoras { get; set; } // cada cuántas horas
    public decimal Valor { get; set; } // Valor del medicamento
    public List<RegistroToma> Historial { get; set; } = new();
}

class RegistroToma
{
    public DateTime FechaHora { get; set; }
    public bool Tomado { get; set; }
}

class Program
{
    static List<Paciente> pacientes = new();
    static ConsoleColor colorMenu = ConsoleColor.Blue;
    static ConsoleColor colorExito = ConsoleColor.Green;
    static ConsoleColor colorOlvido = ConsoleColor.Red;

    static void Main()
    {
        // Ejemplo: dos pacientes German con apellidos distintos
        if (pacientes.Count == 0)
        {
            pacientes.Add(new Paciente
            {
                Nombre = "German",
                Apellido = "Pérez",
                Edad = 35,
                Medicamentos = new List<Medicamento>
                {
                    new Medicamento
                    {
                        Nombre = "Paracetamol",
                        Dosis = "500mg",
                        FrecuenciaHoras = 8,
                        Valor = 50,
                        Historial = new List<RegistroToma>
                        {
                            new RegistroToma { FechaHora = DateTime.Now.AddHours(-7), Tomado = true }
                        }
                    }
                }
            });
            pacientes.Add(new Paciente
            {
                Nombre = "German",
                Apellido = "López",
                Edad = 40,
                Medicamentos = new List<Medicamento>
                {
                    new Medicamento
                    {
                        Nombre = "Ibuprofeno",
                        Dosis = "400mg",
                        FrecuenciaHoras = 12,
                        Valor = 80,
                        Historial = new List<RegistroToma>
                        {
                            new RegistroToma { FechaHora = DateTime.Now.AddHours(-10), Tomado = true }
                        }
                    }
                }
            });
        }

        while (true)
        {
            Console.Clear();
            Console.ForegroundColor = colorMenu;
            Console.WriteLine("==============================");
            Console.WriteLine("  SISTEMA DE RECORDATORIO MÉDICO");
            Console.WriteLine("==============================");
            Console.ResetColor();
            Console.WriteLine("¿Eres paciente o doctor?");
            Console.WriteLine("1. Paciente");
            Console.WriteLine("2. Doctor");
            Console.WriteLine("3. Salir");
            Console.Write("Seleccione una opción: ");
            string opcion = Console.ReadLine()!;
            switch (opcion)
            {
                case "1": FlujoPaciente(); break;
                case "2": FlujoDoctor(); break;
                case "3":
                    Console.WriteLine("¡Gracias por usar el sistema!");
                    return;
                default:
                    Console.WriteLine("Opción no válida. Presione una tecla para continuar...");
                    Console.ReadKey();
                    break;
            }
        }
    }

    static void FlujoPaciente()
    {
        Console.Clear();
        Console.WriteLine("--- Acceso de Paciente ---");
        Console.Write("Ingrese su nombre: ");
        string nombre = Console.ReadLine()!;
        var coincidencias = pacientes.FindAll(p => p.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase));
        if (coincidencias.Count == 0)
        {
            Console.ForegroundColor = colorOlvido;
            Console.WriteLine("No se encontraron pacientes con ese nombre.");
            Console.ResetColor();
            Console.WriteLine("Presione una tecla para continuar...");
            Console.ReadKey();
            return;
        }
        Console.WriteLine("Seleccione su apellido:");
        for (int i = 0; i < coincidencias.Count; i++)
            Console.WriteLine($"{i + 1}. {coincidencias[i].Nombre} {coincidencias[i].Apellido}");
        int idx = int.TryParse(Console.ReadLine(), out int sel) ? sel - 1 : -1;
        if (idx < 0 || idx >= coincidencias.Count)
        {
            Console.WriteLine("Selección inválida.");
            Console.WriteLine("Presione una tecla para continuar...");
            Console.ReadKey();
            return;
        }
        var paciente = coincidencias[idx];
        if (paciente.Medicamentos.Count == 0)
        {
            Console.WriteLine("No tienes medicamentos registrados.");
            Console.WriteLine("Presione una tecla para continuar...");
            Console.ReadKey();
            return;
        }
        Console.ForegroundColor = colorMenu;
        Console.WriteLine($"\nMedicamentos de {paciente.Nombre} {paciente.Apellido}:");
        Console.ResetColor();
        foreach (var med in paciente.Medicamentos)
        {
            DateTime ultimaToma = med.Historial.Count > 0 ? med.Historial[^1].FechaHora : DateTime.Now;
            DateTime proximaToma = ultimaToma.AddHours(med.FrecuenciaHoras);
            TimeSpan restante = proximaToma - DateTime.Now;
            Console.WriteLine("-----------------------------------");
            Console.WriteLine($"Medicamento: {med.Nombre}");
            Console.WriteLine($"Dosis: {med.Dosis}");
            Console.WriteLine($"Valor: ${med.Valor:F2}");
            Console.WriteLine($"Frecuencia: cada {med.FrecuenciaHoras} horas");
            Console.WriteLine($"Próxima toma: {proximaToma:dd/MM/yyyy HH:mm}");
            Console.WriteLine(restante.TotalHours > 0
                ? $"Faltan: {restante.Hours} horas y {restante.Minutes} minutos"
                : "¡Ya es hora de tomar el medicamento!");
        }
        Console.WriteLine("-----------------------------------");
        Console.WriteLine("Presione una tecla para continuar...");
        Console.ReadKey();
    }

    static void FlujoDoctor()
    {
        while (true)
        {
            Console.Clear();
            Console.ForegroundColor = colorMenu;
            Console.WriteLine("--- Menú Doctor ---");
            Console.ResetColor();
            Console.WriteLine("1. Registrar nuevo paciente");
            Console.WriteLine("2. Ver pacientes y medicamentos");
            Console.WriteLine("3. Volver al inicio");
            Console.Write("Seleccione una opción: ");
            string opcion = Console.ReadLine()!;
            switch (opcion)
            {
                case "1": RegistrarPacienteDoctor(); break;
                case "2": VerPacientesYMedicamentos(); break;
                case "3": return;
                default:
                    Console.WriteLine("Opción no válida. Presione una tecla para continuar...");
                    Console.ReadKey();
                    break;
            }
        }
    }

    static void RegistrarPacienteDoctor()
    {
        Console.WriteLine("\n--- Registro de Paciente ---");
        Console.Write("Nombre: ");
        string nombre = Console.ReadLine()!;
        Console.Write("Apellido: ");
        string apellido = Console.ReadLine()!;
        Console.Write("Edad: ");
        int edad = int.TryParse(Console.ReadLine(), out int e) ? e : 0;
        Console.Write("Condición médica (opcional): ");
        string? condicion = Console.ReadLine();
        var paciente = new Paciente { Nombre = nombre, Apellido = apellido, Edad = edad, CondicionMedica = condicion };
        pacientes.Add(paciente);
        Console.ForegroundColor = colorExito;
        Console.WriteLine("Paciente registrado exitosamente.");
        Console.ResetColor();
        Console.WriteLine("¿Desea registrar un medicamento para este paciente? (s/n): ");
        string resp = Console.ReadLine()!.ToLower();
        if (resp == "s")
            RegistrarMedicamentoDoctor(paciente);
        Console.WriteLine("Presione una tecla para continuar...");
        Console.ReadKey();
    }

    static void RegistrarMedicamentoDoctor(Paciente paciente)
    {
        Console.WriteLine("\n--- Registro de Medicamento ---");
        Console.Write("Nombre del medicamento: ");
        string nombre = Console.ReadLine()!;
        Console.Write("Dosis: ");
        string dosis = Console.ReadLine()!;
        Console.Write("Frecuencia (horas entre tomas): ");
        int frecuencia = int.TryParse(Console.ReadLine(), out int f) ? f : 0;
        Console.Write("Valor del medicamento: ");
        decimal valor = decimal.TryParse(Console.ReadLine(), out decimal v) ? v : 0;
        paciente.Medicamentos.Add(new Medicamento
        {
            Nombre = nombre,
            Dosis = dosis,
            FrecuenciaHoras = frecuencia,
            Valor = valor
        });
        Console.ForegroundColor = colorExito;
        Console.WriteLine("Medicamento registrado exitosamente.");
        Console.ResetColor();
    }

    static void VerPacientesYMedicamentos()
    {
        if (pacientes.Count == 0)
        {
            Console.ForegroundColor = colorOlvido;
            Console.WriteLine("No hay pacientes registrados.");
            Console.ResetColor();
            Console.WriteLine("Presione una tecla para continuar...");
            Console.ReadKey();
            return;
        }
        Console.WriteLine("\n--- Lista de Pacientes ---");
        foreach (var paciente in pacientes)
        {
            Console.WriteLine($"Paciente: {paciente.Nombre} {paciente.Apellido} | Edad: {paciente.Edad} | Condición: {paciente.CondicionMedica}");
            if (paciente.Medicamentos.Count == 0)
            {
                Console.WriteLine("  Sin medicamentos registrados.");
            }
            else
            {
                foreach (var med in paciente.Medicamentos)
                {
                    Console.WriteLine($"  Medicamento: {med.Nombre} | Dosis: {med.Dosis} | Frecuencia: {med.FrecuenciaHoras}h | Valor: ${med.Valor:F2}");
                }
            }
            Console.WriteLine("-----------------------------------");
        }
        Console.WriteLine("Presione una tecla para continuar...");
        Console.ReadKey();
    }
}
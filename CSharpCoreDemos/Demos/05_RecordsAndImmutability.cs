// ============================================================
// 核心知识点：Record、不可变性、值相等性
// ============================================================
// 核心问题：
// 1. record class vs record struct vs class 区别？
// 2. with 表达式原理？
// 3. 值相等性 vs 引用相等性？
// 4. init-only setter 有什么用？
// 5. readonly struct 和 record 的关系？
// ============================================================

namespace CSharpCoreDemos.Demos;

public static class RecordsAndImmutabilityDemo
{
    public static void Run()
    {
        Console.WriteLine("═══ Record 与不可变性 ═══\n");

        // === 1. record 的值相等性 ===
        Console.WriteLine("--- 1. 值相等性 ---");

        var p1 = new PersonRecord("Alice", 30);
        var p2 = new PersonRecord("Alice", 30);
        var p3 = new PersonClass("Alice", 30);
        var p4 = new PersonClass("Alice", 30);

        Console.WriteLine($"  record 值相等: p1 == p2 = {p1 == p2}");       // True
        Console.WriteLine($"  record Equals:  p1.Equals(p2) = {p1.Equals(p2)}"); // True
        Console.WriteLine($"  record GetHashCode: {p1.GetHashCode()} == {p2.GetHashCode()}");

        Console.WriteLine($"  class 引用相等: p3 == p4 = {object.ReferenceEquals(p3, p4)}"); // False
        Console.WriteLine($"  class Equals: p3.Equals(p4) = {p3.Equals(p4)}");    // False (未重写)
        Console.WriteLine();

        // === 2. with 表达式（非破坏性变异） ===
        Console.WriteLine("--- 2. with 表达式 ---");

        var original = new PersonRecord("Bob", 25);
        var updated = original with { Age = 26 };

        Console.WriteLine($"  原始: {original}");
        Console.WriteLine($"  更新: {updated}");
        Console.WriteLine($"  引用不同: {ReferenceEquals(original, updated)}\n");

        // === 3. 嵌套记录的 with 表达式 ===
        Console.WriteLine("--- 3. 嵌套记录的 with（浅拷贝） ---");

        var addr = new Address("北京", "朝阳区");
        var emp1 = new Employee("Alice", addr);
        var emp2 = emp1 with { }; // 浅拷贝！地址引用相同

        Console.WriteLine($"  嵌套 with 浅拷贝: {ReferenceEquals(emp1.Address, emp2.Address)}\n");

        // === 4. record struct ===
        Console.WriteLine("--- 4. record struct ---");

        var point1 = new PointStruct(10, 20);
        var point2 = new PointStruct(10, 20);
        Console.WriteLine($"  record struct 值相等: {point1 == point2}");    // True
        Console.WriteLine($"  record struct 是值类型: {typeof(PointStruct).IsValueType}\n");

        // === 5. readonly record struct ===
        Console.WriteLine("--- 5. readonly record struct ---");

        var immutablePoint = new ImmutablePoint(3, 4);
        // immutablePoint.X = 5; // ❌ 编译错误：readonly
        Console.WriteLine($"  readonly record struct: {immutablePoint}\n");

        // === 6. init-only setter ===
        Console.WriteLine("--- 6. init-only setter ---");

        var config = new AppConfig { AppName = "MyApp", Version = "1.0" };
        // config.Version = "2.0"; // ❌ 编译错误：init-only!
        Console.WriteLine($"  init: {config.AppName} v{config.Version}\n");

        // === 7. 反编译看看 record 生成了什么 ===
        Console.WriteLine("--- 7. record 编译器生成 ---");
        Console.WriteLine("  record 编译器会生成:");
        Console.WriteLine("  - Equals() / GetHashCode() / == / !=");
        Console.WriteLine("  - ToString() (打印所有属性)");
        Console.WriteLine("  - Deconstruct (解构方法)");
        Console.WriteLine("  - Clone 方法 (with 表达式用)");
        Console.WriteLine();

        // 解构演示
        var (name, age) = p1;
        Console.WriteLine($"  解构: name={name}, age={age}\n");

        // === 8. 性能对比：class vs record ===
        Console.WriteLine("--- 8. 性能注意事项 ---");
        Console.WriteLine("  class:  引用类型，堆分配，GC 管理");
        Console.WriteLine("  record: 默认引用类型，堆分配，值相等性有额外开销");
        Console.WriteLine("  record struct: 值类型，栈分配，适合小数据 (<16 bytes)");
        Console.WriteLine("  readonly record struct: 不可变值类型，适合 DTO\n");

        // === 9. 位置记录 vs 属性记录 ===
        Console.WriteLine("--- 9. 位置记录 vs 属性记录 ---");

        // 位置记录：主构造函数语法
        var posRecord = new PositionalRecord("K1", "V1");
        // 属性记录：标准属性语法
        var propRecord = new PropertyRecord { Key = "K2", Value = "V2" };

        Console.WriteLine($"  位置记录: {posRecord}");
        Console.WriteLine($"  属性记录: {propRecord}");
    }
}

// === 对比模型 ===

public record PersonRecord(string Name, int Age);

public class PersonClass
{
    public string Name { get; init; }
    public int Age { get; init; }

    public PersonClass(string name, int age) => (Name, Age) = (name, age);
}

public record Address(string City, string District);

public record Employee(string Name, Address Address);

// record struct
public record struct PointStruct(int X, int Y);

public readonly record struct ImmutablePoint(int X, int Y);

// init-only 属性（非 record 也能用）
public class AppConfig
{
    public string AppName { get; init; } = "";
    public string Version { get; init; } = "";
}

// 位置记录
public record PositionalRecord(string Key, string Value);

// 属性记录
public record PropertyRecord
{
    public string Key { get; init; } = "";
    public string Value { get; init; } = "";
}

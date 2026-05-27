// ============================================================
// 核心知识点：泛型、协变、逆变、约束
// ============================================================
// 核心问题：
// 1. 泛型约束有哪些？each 约束的作用？
// 2. 协变 (out) 和逆变 (in) 是什么？
// 3. List<Dog> 能赋值给 List<Animal> 吗？
// 4. 泛型方法的类型推断？
// 5. 泛型在 AOT/运行时如何工作？（类型擦除 vs 特化）
// ============================================================

namespace CSharpCoreDemos.Demos;

public static class GenericsAndVarianceDemo
{
    public static void Run()
    {
        Console.WriteLine("═══ 泛型、协变与逆变 ═══\n");

        // === 1. 泛型约束 ===
        Console.WriteLine("--- 1. 泛型约束 ---");

        var repo = new Repository<Entity>();
        repo.Add(new Entity(1, "实体1"));
        var entity = repo.GetById(1);
        Console.WriteLine($"  Generic Repository: {entity}\n");

        // === 2. 类型推断 ===
        Console.WriteLine("--- 2. 泛型方法类型推断 ---");

        // 显式指定类型参数
        var result1 = GenericUtils.CreatePair<int, string>(1, "one");
        // 编译器自动推断类型
        var result2 = GenericUtils.CreatePair(2, "two");
        Console.WriteLine($"  推断: {result2}\n");

        // === 3. 协变 Covariance (out) ===
        Console.WriteLine("--- 3. 协变 (out) ---");
        // out 关键字表示类型参数只用于输出位置
        // IEnumerable<out T> → 可以把 IEnumerable<Dog> 当作 IEnumerable<Animal>

        IEnumerable<Dog> dogs = new List<Dog> { new Dog(), new Dog() };
        IEnumerable<Animal> animals = dogs; // ✅ 协变：子类→父类
        Console.WriteLine("  IEnumerable<Dog> → IEnumerable<Animal> ✅\n");

        // === 4. 逆变 Contravariance (in) ===
        Console.WriteLine("--- 4. 逆变 (in) ---");
        // in 关键字表示类型参数只用于输入位置
        // IComparer<in T> → 可以把 IComparer<Animal> 当作 IComparer<Dog>

        IComparer<Animal> animalComparer = new AnimalComparer();
        IComparer<Dog> dogComparer = animalComparer; // ✅ 逆变：父类→子类
        Console.WriteLine("  IComparer<Animal> → IComparer<Dog> ✅\n");

        // === 5. 不变性 Invariance ===
        Console.WriteLine("--- 5. 不变性 ---");
        // List<T> 没有 in/out，既不协变也不逆变
        var dogList = new List<Dog>();
        // List<Animal> animalList = dogList; // ❌ 编译错误！
        Console.WriteLine("  List<Dog> → List<Animal> ❌ (不变)\n");

        // === 6. 自定义泛型接口的协变/逆变 ===
        Console.WriteLine("--- 6. 自定义协变/逆变 ---");

        IProducer<Animal> producer = new Producer<Dog>(); // ✅ 协变
        var produced = producer.Produce();
        Console.WriteLine($"  IProducer<out T>: {produced.GetType().Name}");

        IConsumer<Dog> consumer = new Consumer<Animal>(); // ✅ 逆变
        consumer.Consume(new Dog());
        Console.WriteLine("  IConsumer<in T>: Consumer<Animal> → IConsumer<Dog>\n");

        // === 7. 静态字段和泛型 ===
        Console.WriteLine("--- 7. 泛型的静态字段 ---");
        // 重点：每个封闭泛型类型都有自己的静态字段

        GenericCounter<int>.Count = 10;
        GenericCounter<string>.Count = 20;

        Console.WriteLine($"  GenericCounter<int>.Count = {GenericCounter<int>.Count}");
        Console.WriteLine($"  GenericCounter<string>.Count = {GenericCounter<string>.Count}");
        Console.WriteLine("  知识点: 每个封闭泛型类型独立分配静态字段\n");

        // === 8. where 约束 ===
        Console.WriteLine("--- 8. where 约束类型 ---");
        Console.WriteLine("  where T : class           → 引用类型约束");
        Console.WriteLine("  where T : struct          → 值类型约束");
        Console.WriteLine("  where T : new()           → 无参构造函数约束");
        Console.WriteLine("  where T : ISomeInterface  → 接口约束");
        Console.WriteLine("  where T : SomeBaseClass   → 基类约束");
        Console.WriteLine("  where T : U               → 裸类型约束（T 继承自 U）");
        Console.WriteLine("  where T : notnull         → 不可为 null 约束 (C# 8+)");
        Console.WriteLine("  where T : unmanaged       → 非托管类型 (C# 7.3+)");
        Console.WriteLine("  where T : allows ref struct → 允许引用结构 (C# 13)");
    }
}

// === 数据模型 ===

public class Animal
{
    public virtual string Sound() => "...";
}

public class Dog : Animal
{
    public override string Sound() => "Woof!";

    public void Fetch() => Console.WriteLine("  Dog fetching...");
}

// === 泛型约束示例 ===

public interface IEntity
{
    int Id { get; }
}

public record Entity(int Id, string Name) : IEntity;

public class Repository<T> where T : class, IEntity
{
    private readonly Dictionary<int, T> _store = new();

    public void Add(T item) => _store[item.Id] = item;

    public T? GetById(int id) => _store.GetValueOrDefault(id);
}

// === 静态字段和泛型 ===

public static class GenericCounter<T>
{
    public static int Count;
}

// === 协变/逆变接口示例 ===

// 协变：T 只出现在输出位置
public interface IProducer<out T>
{
    T Produce();
}

// 逆变：T 只出现在输入位置
public interface IConsumer<in T>
{
    void Consume(T item);
}

public class Producer<T> : IProducer<T> where T : new()
{
    public T Produce() => new();
}

public class Consumer<T> : IConsumer<T>
{
    public void Consume(T item) => Console.WriteLine($"  Consumed: {item?.GetType().Name}");
}

// 逆变使用示例：比较器
public class AnimalComparer : IComparer<Animal>
{
    public int Compare(Animal? x, Animal? y) => 0; // 简化
}

// === 通用工具类 ===

public static class GenericUtils
{
    public static Pair<T1, T2> CreatePair<T1, T2>(T1 first, T2 second) =>
        new Pair<T1, T2>(first, second);
}

public record Pair<T1, T2>(T1 First, T2 Second);

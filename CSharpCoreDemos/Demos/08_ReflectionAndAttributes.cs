// ============================================================
// 核心知识点：反射、特性 (Attribute)、动态编程
// ============================================================
// 核心问题：
// 1. 反射的用途和性能代价？
// 2. 如何自定义 Attribute？
// 3. 运行时读取 Attribute？
// 4. 反射的缓存策略？
// 5. 反射在框架中的应用 (ORM/DI/Serializer)？
// ============================================================

using System.Reflection;

namespace CSharpCoreDemos.Demos;

public static class ReflectionDemo
{
    public static void Run()
    {
        Console.WriteLine("═══ 反射与特性 ═══\n");

        // === 1. 自定义 Attribute 的使用 ===
        Console.WriteLine("--- 1. 自定义 Attribute ---");

        var service = new OrderService();
        var typeInfo = service.GetType();

        // 读取类级别的 Attribute
        var classAttr = typeInfo.GetCustomAttribute<ServiceAttribute>();
        Console.WriteLine($"  Service: {classAttr?.Name} v{classAttr?.Version}");

        // 读取方法级别的 Attribute
        var methods = typeInfo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<PermissionAttribute>();
            if (attr != null)
            {
                Console.WriteLine($"  方法: {method.Name}, 所需权限: {attr.RequiredRole}, 操作: {attr.Action}");
            }
        }
        Console.WriteLine();

        // === 2. 反射调用方法 ===
        Console.WriteLine("--- 2. 反射调用方法 ---");

        var instance = Activator.CreateInstance(typeof(OrderService)) as OrderService;
        var methodInfo = typeof(OrderService).GetMethod("ProcessOrder");
        if (methodInfo != null)
        {
            var result = methodInfo.Invoke(instance, new object[] { "ORD-001", 100m });
            Console.WriteLine($"  反射调用结果: {result}");
        }
        Console.WriteLine();

        // === 3. 反射读写属性 ===
        Console.WriteLine("--- 3. 反射读写属性 ---");

        var order = new OrderModel { Id = 1, CustomerName = "Alice", TotalAmount = 99.9m };
        var props = order.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            var value = prop.GetValue(order);
            Console.WriteLine($"  属性: {prop.Name} = {value} (类型: {prop.PropertyType.Name})");
        }
        Console.WriteLine();

        // === 4. 基于 Attribute 的验证 ===
        Console.WriteLine("--- 4. 基于 Attribute 的数据验证 ---");

        var validUser = new UserModel { Name = "Alice", Email = "alice@example.com", Age = 25 };
        var invalidUser = new UserModel { Name = "", Email = "not-an-email", Age = -1 };

        Console.WriteLine($"  有效用户验证: {Validate(validUser)}");
        Console.WriteLine("  无效用户验证结果:");
        var errors = ValidateWithErrors(invalidUser);
        foreach (var err in errors) Console.WriteLine($"    ❌ {err}");
        Console.WriteLine();

        // === 5. 反射的性能代价与缓存策略 ===
        Console.WriteLine("--- 5. 反射性能与缓存 ---");

        const int iterations = 100_000;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 直接调用
        for (int i = 0; i < iterations; i++)
            service.ProcessOrder("test", 10m);

        sw.Stop();
        Console.WriteLine($"  直接调用 ×{iterations}: {sw.ElapsedMilliseconds}ms");

        // 反射调用（每次 GetMethod）
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            var m = typeof(OrderService).GetMethod("ProcessOrder");
            m?.Invoke(service, new object[] { "test", 10m });
        }
        sw.Stop();
        Console.WriteLine($"  反射调用 (无缓存) ×{iterations}: {sw.ElapsedMilliseconds}ms");

        // 反射调用（缓存 MethodInfo + Delegate）
        sw.Restart();
        var cachedMethod = typeof(OrderService).GetMethod("ProcessOrder");
        var cachedDelegate = (Func<string, decimal, string>)Delegate.CreateDelegate(
            typeof(Func<string, decimal, string>), service, cachedMethod!);

        for (int i = 0; i < iterations; i++)
            cachedDelegate("test", 10m);

        sw.Stop();
        Console.WriteLine($"  反射调用 (Delegate缓存) ×{iterations}: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine("  ✅ 结论: Delegate.CreateDelegate 几乎无性能损耗\n");

        // === 6. Assembly 信息 ===
        Console.WriteLine("--- 6. Assembly 信息 ---");

        var asm = Assembly.GetExecutingAssembly();
        Console.WriteLine($"  程序集: {asm.GetName().Name}");
        Console.WriteLine($"  版本: {asm.GetName().Version}");
        Console.WriteLine($"  位置: {asm.Location}");
        Console.WriteLine($"  包含类型数: {asm.GetTypes().Length}\n");

        // === 7. 反射应用场景 ===
        Console.WriteLine("--- 7. 反射的典型应用场景 ---");
        Console.WriteLine("  1. ORM 框架: EF Core 的实体映射");
        Console.WriteLine("  2. DI 容器: 自动注册和注入");
        Console.WriteLine("  3. 序列化/反序列化: JSON/XML 序列化器");
        Console.WriteLine("  4. 配置映射: appsettings.json 到强类型对象");
        Console.WriteLine("  5. 测试框架: xUnit 自动发现[Fact]方法");
        Console.WriteLine("  6. AOP: 动态代理、拦截器 (Castle.Core)");
    }

    // 基于 Attribute 的验证引擎
    private static bool Validate<T>(T obj)
    {
        var props = typeof(T).GetProperties();
        foreach (var prop in props)
        {
            // Required 验证
            if (prop.GetCustomAttribute<RequiredAttribute>() != null)
            {
                var value = prop.GetValue(obj) as string;
                if (string.IsNullOrWhiteSpace(value)) return false;
            }

            // Email 验证
            if (prop.GetCustomAttribute<EmailAttribute>() != null)
            {
                var value = prop.GetValue(obj) as string;
                if (value == null || !value.Contains('@')) return false;
            }

            // Range 验证
            var rangeAttr = prop.GetCustomAttribute<RangeAttribute>();
            if (rangeAttr != null)
            {
                var value = (int)prop.GetValue(obj)!;
                if (value < rangeAttr.Min || value > rangeAttr.Max) return false;
            }
        }
        return true;
    }

    private static List<string> ValidateWithErrors<T>(T obj)
    {
        var errors = new List<string>();
        var props = typeof(T).GetProperties();

        foreach (var prop in props)
        {
            if (prop.GetCustomAttribute<RequiredAttribute>() != null)
            {
                var value = prop.GetValue(obj) as string;
                if (string.IsNullOrWhiteSpace(value))
                    errors.Add($"{prop.Name} 是必填项");
            }

            if (prop.GetCustomAttribute<EmailAttribute>() != null)
            {
                var value = prop.GetValue(obj) as string;
                if (value == null || !value.Contains('@'))
                    errors.Add($"{prop.Name} 邮箱格式不正确");
            }

            var rangeAttr = prop.GetCustomAttribute<RangeAttribute>();
            if (rangeAttr != null)
            {
                var value = (int)prop.GetValue(obj)!;
                if (value < rangeAttr.Min || value > rangeAttr.Max)
                    errors.Add($"{prop.Name} 必须在 {rangeAttr.Min}-{rangeAttr.Max} 之间");
            }
        }

        return errors;
    }
}

// === 自定义 Attribute ===

[AttributeUsage(AttributeTargets.Class)]
public class ServiceAttribute(string name, string version) : Attribute
{
    public string Name { get; } = name;
    public string Version { get; } = version;
}

[AttributeUsage(AttributeTargets.Method)]
public class PermissionAttribute(string requiredRole, string action) : Attribute
{
    public string RequiredRole { get; } = requiredRole;
    public string Action { get; } = action;
}

// 验证 Attribute
public class RequiredAttribute : Attribute { }

public class EmailAttribute : Attribute { }

public class RangeAttribute(int min, int max) : Attribute
{
    public int Min { get; } = min;
    public int Max { get; } = max;
}

// === 业务类 ===

[Service("订单服务", "2.0")]
public class OrderService
{
    [Permission("admin", "create")]
    public string ProcessOrder(string orderId, decimal amount)
    {
        return $"订单 {orderId} 处理完成，金额: {amount:C}";
    }

    [Permission("user", "read")]
    public string GetOrder(string orderId) => $"订单 {orderId}";
}

public class OrderModel
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal TotalAmount { get; set; }
}

public class UserModel
{
    [Required]
    public string Name { get; set; } = "";

    [Email]
    public string Email { get; set; } = "";

    [Range(0, 150)]
    public int Age { get; set; }
}

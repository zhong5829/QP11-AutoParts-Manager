using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Dapper;

namespace QP11.Data.Infrastructure;

public class FallbackTypeMapper : SqlMapper.ITypeMap
{
    private readonly IEnumerable<SqlMapper.ITypeMap> _mappers;

    public FallbackTypeMapper(IEnumerable<SqlMapper.ITypeMap> mappers)
    {
        _mappers = mappers;
    }

    public ConstructorInfo? FindConstructor(string[] names, Type[] types)
    {
        foreach (var mapper in _mappers)
        {
            var result = mapper.FindConstructor(names, types);
            if (result != null) return result;
        }
        return null;
    }

    public ConstructorInfo? FindExplicitConstructor()
    {
        return _mappers
            .Select(m => m.FindExplicitConstructor())
            .FirstOrDefault(r => r != null);
    }

    public SqlMapper.IMemberMap? GetConstructorParameter(ConstructorInfo constructor, string columnName)
    {
        foreach (var mapper in _mappers)
        {
            var result = mapper.GetConstructorParameter(constructor, columnName);
            if (result != null) return result;
        }
        return null;
    }

    public SqlMapper.IMemberMap? GetMember(string columnName)
    {
        foreach (var mapper in _mappers)
        {
            var result = mapper.GetMember(columnName);
            if (result != null) return result;
        }
        return null;
    }
}

public class ColumnAttributeTypeMapper<T> : FallbackTypeMapper
{
    public ColumnAttributeTypeMapper()
        : base(new SqlMapper.ITypeMap[]
        {
            new ColumnAttributeMemberMapper(typeof(T)),
            new DefaultTypeMap(typeof(T))
        })
    {
    }

    private class ColumnAttributeMemberMapper : SqlMapper.ITypeMap
    {
        private readonly Type _type;

        public ColumnAttributeMemberMapper(Type type)
        {
            _type = type;
        }

        public ConstructorInfo? FindConstructor(string[] names, Type[] types)
        {
            return _type.GetConstructor(Type.EmptyTypes);
        }

        public ConstructorInfo? FindExplicitConstructor()
        {
            return null;
        }

        public SqlMapper.IMemberMap? GetConstructorParameter(ConstructorInfo constructor, string columnName)
        {
            return null;
        }

        public SqlMapper.IMemberMap? GetMember(string columnName)
        {
            var property = _type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p =>
                {
                    var attr = p.GetCustomAttribute<ColumnAttribute>();
                    if (attr != null && string.Equals(attr.Name, columnName, StringComparison.OrdinalIgnoreCase))
                        return true;
                    return string.Equals(p.Name, columnName, StringComparison.OrdinalIgnoreCase);
                });

            return property != null ? new MemberMap(property) : null;
        }
    }

    private class MemberMap : SqlMapper.IMemberMap
    {
        private readonly PropertyInfo _property;

        public MemberMap(PropertyInfo property)
        {
            _property = property;
        }

        public string ColumnName => _property.Name;
        public MemberInfo Member => _property;
        public FieldInfo? Field => null;
        public ParameterInfo? Parameter => null;
        public PropertyInfo? Property => _property;
        public Type MemberType => _property.PropertyType;
    }
}

public static class DapperTypeMapper
{
    public static void Register<T>()
    {
        SqlMapper.SetTypeMap(typeof(T), new ColumnAttributeTypeMapper<T>());
    }
}

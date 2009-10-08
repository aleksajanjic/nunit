// ***********************************************************************
// Copyright (c) 2007 Charlie Poole
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System;
using System.Reflection;
using System.Collections;

namespace NUnit.Core
{
	/// <summary>
	/// Helper methods for inspecting a type by reflection. 
	/// 
	/// Many of these methods take ICustomAttributeProvider as an 
	/// argument to avoid duplication, even though certain attributes can 
	/// only appear on specific types of members, like MethodInfo or Type.
	/// 
	/// In the case where a type is being examined for the presence of
	/// an attribute, interface or named member, the Reflect methods
	/// operate with the full name of the member being sought. This
	/// removes the necessity of the caller having a reference to the
	/// assembly that defines the item being sought and allows the
	/// NUnit core to inspect assemblies that reference an older
	/// version of the NUnit framework.
	/// </summary>
	public class Reflect
	{
        private static readonly BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static Hashtable topAttributes = new Hashtable();
        private static Hashtable allAttributes = new Hashtable();
        private static Hashtable methodCache = new Hashtable();

        #region Attributes 

        /// <summary>
        /// Check presence of attribute with a given name on a member.
        /// </summary>
        /// <param name="member">The member to examine</param>
        /// <param name="attrName">The FullName of the attribute type to look for</param>
        /// <param name="inherit">True to include inherited attributes</param>
        /// <returns>True if the attribute is present</returns>
        private static bool HasAttribute(ICustomAttributeProvider member, string attrName, bool inherit)
        {
            foreach (Attribute attribute in GetAttributes(member, inherit))
                if (IsInstanceOfType(attrName, attribute))
                    return true;
            return false;
        }
        private static bool HasAttribute(ICustomAttributeProvider member, Type attributeType, bool inherit)
        {
            return member.IsDefined(attributeType, inherit);
        }

        /// <summary>
        /// Get attribute of a given type on a member. If multiple attributes
        /// of a type are present, the first one found is returned.
        /// </summary>
        /// <param name="member">The member to examine</param>
        /// <param name="attributeType">The attribute Type to look for</param>
        /// <param name="inherit">True to include inherited attributes</param>
        /// <returns>The attribute or null</returns>
        public static System.Attribute GetAttribute(ICustomAttributeProvider member, Type attributeType, bool inherit)
        {
            object[] attrs = member.GetCustomAttributes(attributeType, inherit);
            return attrs.Length > 0 ? (Attribute)attrs[0] : null;
        }

        /// <summary>
		/// Get all attributes of a given type on a member.
		/// </summary>
		/// <param name="member">The member to examine</param>
		/// <param name="attribueType">The attribute Type to look for</param>
		/// <param name="inherit">True to include inherited attributes</param>
		/// <returns>The attribute or null</returns>
        public static System.Attribute[] GetAttributes(
            ICustomAttributeProvider member, Type attributeType, bool inherit)
        {
            return (System.Attribute[])member.GetCustomAttributes(attributeType, inherit);
        }

        /// <summary>
        /// Get all attributes on a member.
        /// </summary>
        /// <param name="member">The member to examine</param>
        /// <param name="inherit">True to include inherited attributes</param>
        /// <returns>The attribute or null</returns>
        public static System.Attribute[] GetAttributes(
            ICustomAttributeProvider member, bool inherit)
        {
            //Hashtable attributeCache = inherit ? allAttributes : topAttributes;

            //if (attributeCache.Contains(member))
            //    return attributeCache[member] as Attribute[];

            object[] attributes = member.GetCustomAttributes(inherit);
            System.Attribute[] result = new System.Attribute[attributes.Length];
            int n = 0;
            foreach (Attribute attribute in attributes)
                result[n++] = attribute;

            //attributeCache[member] = result;

            return result;
        }

        #endregion

		#region Interfaces

		/// <summary>
		/// Check to see if a type implements a named interface.
		/// </summary>
		/// <param name="fixtureType">The type to examine</param>
		/// <param name="interfaceName">The FullName of the interface to check for</param>
		/// <returns>True if the interface is implemented by the type</returns>
        public static bool HasInterface(Type fixtureType, string interfaceName)
        {
            foreach (Type type in fixtureType.GetInterfaces())
                if (type.FullName == interfaceName)
                    return true;
            return false;
        }
        public static bool HasInterface(Type fixtureType, Type interfaceType)
        {
            foreach (Type type in fixtureType.GetInterfaces())
                if (type == interfaceType)
                    return true;
            return false;
        }

		#endregion

		#region Inheritance
		//SHMARYA: [ 10/12/2005 ]
		/// <summary>
		/// Checks to see if a type inherits from a named type. 
		/// </summary>
		/// <param name="type">The type to examine</param>
		/// <param name="parentType">The FullName of the inherited type to look for</param>
		/// <returns>True if the type inherits from the named type.</returns>
		public static bool InheritsFrom( Type type, string typeName )
		{
			for( Type current = type; current != typeof( object ); current = current.BaseType )
				if( current.FullName == typeName )
					return true;

			return false;
		}

		public static bool InheritsFrom( object obj, string typeName )
		{
			return InheritsFrom( obj.GetType(), typeName );
		}

		public static bool IsInstanceOfType( string typeName, Attribute attr )
		{
			Type type = attr.GetType();
			return type.FullName == typeName || InheritsFrom( type, typeName );
		}
		#endregion

		#region Get Methods of a type

		/// <summary>
		/// Find the default constructor on a type
		/// </summary>
		/// <param name="fixtureType"></param>
		/// <returns></returns>
		public static ConstructorInfo GetConstructor( Type fixtureType )
		{
			return fixtureType.GetConstructor( Type.EmptyTypes );
		}

		/// <summary>
		/// Find the default constructor on a type
		/// </summary>
		/// <param name="fixtureType"></param>
		/// <returns></returns>
		public static ConstructorInfo GetConstructor( Type fixtureType, Type[] types )
		{
			return fixtureType.GetConstructor( types );
		}

        /// <summary>
        /// Examine a fixture type and return an array of methods having a 
        /// particular attribute. The array is order with base methods first.
        /// </summary>
        /// <param name="fixtureType">The type to examine</param>
        /// <param name="attributeType">The attribute Type to look for</param>
        /// <returns>The array of methods found</returns>
        public static MethodInfo[] GetMethodsWithAttribute(Type fixtureType, Type attributeType, bool inherit)
        {
            ArrayList list = new ArrayList();

            foreach (MethodInfo method in GetMethods(fixtureType))
            {
                if (HasAttribute(method, attributeType, inherit))
                    list.Add(method);
            }

            list.Sort(new BaseTypesFirstComparer());

            return (MethodInfo[])list.ToArray(typeof(MethodInfo));
        }

        private static MethodInfo[] GetMethods(Type fixtureType)
        {
            if (methodCache.Contains(fixtureType))
                return methodCache[fixtureType] as MethodInfo[];

            MethodInfo[] result = fixtureType.GetMethods(AllMembers);
            methodCache[fixtureType] = result;

            return result;
        }

        private class BaseTypesFirstComparer : IComparer
        {
            #region IComparer Members

            public int Compare(object x, object y)
            {
                MethodInfo m1 = x as MethodInfo;
                MethodInfo m2 = y as MethodInfo;

                if (m1 == null || m2 == null) return 0;

                Type m1Type = m1.DeclaringType;
                Type m2Type = m2.DeclaringType;

                if ( m1Type == m2Type ) return 0;
                if ( m1Type.IsAssignableFrom(m2Type) ) return -1;

                return 1;
            }

            #endregion
        }

        /// <summary>
        /// Examine a fixture type and return true if it has a method with
        /// a particular attribute. 
        /// </summary>
        /// <param name="fixtureType">The type to examine</param>
        /// <param name="attributeType">The attribute Type to look for</param>
        /// <returns>True if found, otherwise false</returns>
        public static bool HasMethodWithAttribute(Type fixtureType, Type attributeType, bool inherit)
        {
            foreach (MethodInfo method in GetMethods( fixtureType ))
            {
                if (HasAttribute(method, attributeType, inherit))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Examine a fixture type and get a method with a particular name.
        /// In the case of overloads, the first one found is returned.
        /// </summary>
        /// <param name="fixtureType">The type to examine</param>
        /// <param name="methodName">The name of the method</param>
        /// <returns>A MethodInfo or null</returns>
        public static MethodInfo GetNamedMethod(Type fixtureType, string methodName)
        {
            foreach (MethodInfo method in GetMethods( fixtureType ))
            {
                if (method.Name == methodName)
                    return method;
            }

            return null;
        }

        /// <summary>
        /// Examine a fixture type and get a method with a particular name and list
        /// of arguments. In the case of overloads, the first one found is returned.
        /// </summary>
        /// <param name="fixtureType">The type to examine</param>
        /// <param name="methodName">The name of the method</param>
        /// <param name="argTypes">The full names of the argument types to search for</param>
        /// <returns>A MethodInfo or null</returns>
        public static MethodInfo GetNamedMethod(Type fixtureType, string methodName, 
            string[] argTypes)
        {
            foreach (MethodInfo method in GetMethods(fixtureType) )
            {
                if (method.Name == methodName)
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == argTypes.Length)
                    {
                        bool match = true;
                        for (int i = 0; i < argTypes.Length; i++)
                            if (parameters[i].ParameterType.FullName != argTypes[i])
                            {
                                match = false;
                                break;
                            }

                        if (match)
                            return method;
                    }
                }
            }

            return null;
        }

        #endregion

		#region Get Properties of a type
		/// <summary>
		/// Examine a type and return a property having a particular attribute.
		/// In the case of multiple methods, the first one found is returned.
		/// </summary>
		/// <param name="fixtureType">The type to examine</param>
		/// <param name="attributeType">The attribute Type to look for</param>
		/// <returns>A PropertyInfo or null</returns>
        public static PropertyInfo GetPropertyWithAttribute(Type fixtureType, Type attributeType)
        {
            foreach (PropertyInfo property in fixtureType.GetProperties(AllMembers))
            {
                if (HasAttribute(property, attributeType, true))
                    return property;
            }

            return null;
        }

		/// <summary>
		/// Examine a type and get a property with a particular name.
		/// In the case of overloads, the first one found is returned.
		/// </summary>
		/// <param name="type">The type to examine</param>
		/// <param name="bindingFlags">BindingFlags to use</param>
		/// <returns>A PropertyInfo or null</returns>
		public static PropertyInfo GetNamedProperty( Type type, string name, BindingFlags bindingFlags )
		{
			return type.GetProperty( name, bindingFlags );
		}

		/// <summary>
		/// Get the value of a named property on an object using binding flags of Public and Instance
		/// </summary>
		/// <param name="obj">The object for which the property value is needed</param>
		/// <param name="name">The name of a non-indexed property of the object</param>
		/// <returns></returns>
		public static object GetPropertyValue( object obj, string name )
		{
			return GetPropertyValue( obj, name, BindingFlags.Public | BindingFlags.Instance );
		}

		/// <summary>
		/// Get the value of a named property on an object
		/// </summary>
		/// <param name="obj">The object for which the property value is needed</param>
		/// <param name="name">The name of a non-indexed property of the object</param>
		/// <param name="bindingFlags">BindingFlags for use in determining which properties are needed</param>param>
		/// <returns></returns>
		public static object GetPropertyValue( object obj, string name, BindingFlags bindingFlags )
		{
			PropertyInfo property = GetNamedProperty( obj.GetType(), name, bindingFlags );
			if ( property != null )
				return property.GetValue( obj, null );
			return null;
		}
		#endregion

		#region Invoke Methods

        /// <summary>
        /// Invoke the default constructor on a Type
        /// </summary>
        /// <param name="type">The Type to be constructed</param>
        /// <returns>An instance of the Type</returns>
        public static object Construct(Type type)
        {
            ConstructorInfo ctor = GetConstructor(type);
            if (ctor == null)
                throw new InvalidTestFixtureException(type.FullName + " does not have a default constructor");

            return ctor.Invoke(null);
        }

        /// <summary>
        /// Invoke a constructor on a Type with arguments
        /// </summary>
        /// <param name="type">The Type to be constructed</param>
        /// <param name="arguments">Arguments to the constructor</param>
        /// <returns>An instance of the Type</returns>
        public static object Construct(Type type, object[] arguments)
        {
            if (arguments == null) return Construct(type);

            Type[] argTypes = Type.GetTypeArray(arguments);
            ConstructorInfo ctor = GetConstructor(type, argTypes);
            if (ctor == null)
                throw new InvalidTestFixtureException(type.FullName + " does not have a suitable constructor");

            return ctor.Invoke(arguments);
        }

        /// <summary>
		/// Invoke a parameterless method returning void on an object.
		/// </summary>
		/// <param name="method">A MethodInfo for the method to be invoked</param>
		/// <param name="fixture">The object on which to invoke the method</param>
		public static object InvokeMethod( MethodInfo method, object fixture ) 
		{
			return InvokeMethod( method, fixture, null );
		}

		/// <summary>
		/// Invoke a method returning void, converting any TargetInvocationException
		/// to an NUnitException
		/// </summary>
		/// <param name="method">A MethodInfo for the method to be invoked</param>
		/// <param name="fixture">The object on which to invoke the method</param>
		public static object InvokeMethod( MethodInfo method, object fixture, params object[] args )
		{
			if(method != null)
			{
				try
				{
					return method.Invoke( fixture, args );
				}
				catch(TargetInvocationException e)
				{
					Exception inner = e.InnerException;
					throw new NUnitException("Rethrown",inner);
				}
			}

		    return null;
		}

		#endregion

		#region Private Constructor for static-only class

		private Reflect() { }

		#endregion
	}
}
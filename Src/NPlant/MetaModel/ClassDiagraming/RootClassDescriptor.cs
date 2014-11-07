﻿using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.ServiceModel;
using NPlant.Core;
using NPlant.Generation.ClassDiagraming;

namespace NPlant.MetaModel.ClassDiagraming
{
    public abstract class RootClassDescriptor : IKeyedItem
    {
        private readonly KeyedList<ClassMemberDescriptor> _members = new KeyedList<ClassMemberDescriptor>();
        private readonly KeyedList<ClassMethodDescriptor> _methods = new KeyedList<ClassMethodDescriptor>();

        protected RootClassDescriptor(Type reflectedType)
        {
            this.RenderInheritance = true;
            this.ReflectedType = reflectedType;
            this.Name = this.ReflectedType.GetFriendlyGenericName();
        }

        public void Visit()
        {
            var context = ClassDiagramVisitorContext.Current;
            this.MetaModel = context.GetTypeMetaModel(this.ReflectedType);

            if(context.ShowMembers)
                LoadMembers(context);
            
            if(context.ShowMethods)
                LoadMethods(context);

            var showInheritance = ShouldShowInheritance(context);

            if (!this.MetaModel.Hidden)
            {
                foreach (ClassMemberDescriptor member in this.Members.InnerList)
                {
                    if (this.MetaModel.TreatAllMembersAsPrimitives)
                        member.TreatAsPrimitive = true;

                    TypeMetaModel metaModel = member.MetaModel;

                    if (!metaModel.Hidden && !member.TreatAsPrimitive)
                    {
                        // if not showing inheritance then show all members
                        // otherwise, only show member that aren't inherited
                        if (!showInheritance || !member.IsInherited)
                        {
                            if (metaModel.IsComplexType && ClassDiagram.MemberVisibility[this, member.Key])
                            {
                                var nextLevel = this.Level + 1;

                                if (member.MemberType.IsEnumerable())
                                {
                                    var enumeratorType = member.MemberType.GetEnumeratorType();
                                    var enumeratorTypeMetaModel = context.GetTypeMetaModel(enumeratorType);

                                    if (enumeratorTypeMetaModel.IsComplexType)
                                    {
                                        context.AddRelated(this, enumeratorType.GetReflected(), ClassDiagramRelationshipTypes.HasMany, nextLevel, member.Name);
                                    }
                                }
                                else
                                {
                                    context.AddRelated(this, member.MemberType.GetReflected(), ClassDiagramRelationshipTypes.HasA, nextLevel, member.Name);
                                }
                            }
                        }
                    }
                }
            }

            if (showInheritance)
            {
                context.AddRelated(this, this.ReflectedType.BaseType.GetReflected(), ClassDiagramRelationshipTypes.Base, this.Level - 1);
            }
        }

        private void LoadMethods(ClassDiagramVisitorContext context)
        {
            var methods = this.ReflectedType.GetMethods(context.ShowMethodsBindingFlags);

            foreach (var method in methods)
            {
                if(!method.IsProperty()) // weed up the compiler generated methods for properties
                    _methods.Add(new ClassMethodDescriptor(method));
            }
        }

        private bool ShouldShowInheritance(ClassDiagramVisitorContext context)
        {
            bool showInheritance = this.RenderInheritance && this.ReflectedType.BaseType != null;

            if (showInheritance)
            {
                var baseTypeMetaModel = context.GetTypeMetaModel(this.ReflectedType.BaseType);

                showInheritance = !baseTypeMetaModel.HideAsBaseClass && !baseTypeMetaModel.Hidden;
            }

            return showInheritance;
        }

        protected virtual void LoadMembers(ClassDiagramVisitorContext context)
        {
            switch (context.ScanMode)
            {
                case ClassDiagramScanModes.SystemServiceModelMembers:
                    _members.AddRange(this.ReflectedType.GetFields()
                                                        .Where(x => x.HasAttribute<DataMemberAttribute>() || x.HasAttribute<MessageBodyMemberAttribute>())
                                                        .Where(x => !x.IsDefined(typeof(CompilerGeneratedAttribute), false))
                                                        .Select(field => new ClassMemberDescriptor(this, field))
                                     );
                    _members.AddRange(this.ReflectedType.GetProperties()
                                                        .Where(x => x.HasAttribute<DataMemberAttribute>() || x.HasAttribute<MessageBodyMemberAttribute>())
                                                        .Where(x => !x.IsDefined(typeof(CompilerGeneratedAttribute), false))
                                                        .Select(property => new ClassMemberDescriptor(this, property))
                                     );
                    break;
                case ClassDiagramScanModes.AllMembers:
                    _members.AddRange(this.ReflectedType.GetFields(context.ShowMembersBindingFlags)
                                                        .Where(x => !x.IsDefined(typeof(CompilerGeneratedAttribute), false))
                                                        .Select(field => new ClassMemberDescriptor(this, field))
                                     );
                    _members.AddRange(this.ReflectedType.GetProperties(context.ShowMembersBindingFlags)
                                                        .Where(x => !x.IsDefined(typeof(CompilerGeneratedAttribute), false))
                                                        .Select(property => new ClassMemberDescriptor(this, property))
                                     );
                    break;
                default:
                    _members.AddRange(this.ReflectedType.GetFields()
                                                        .Where(x => !x.IsDefined(typeof(CompilerGeneratedAttribute), false))
                                                        .Select(field => new ClassMemberDescriptor(this, field))
                                     );
                    _members.AddRange(this.ReflectedType.GetProperties()
                                                        .Where(x => !x.IsDefined(typeof(CompilerGeneratedAttribute), false))
                                                        .Select(property => new ClassMemberDescriptor(this, property))
                                     );
                    break;
            }
        }

        string IKeyedItem.Key { get { return this.Name; } }

        public string Name { get; protected set; }

        public bool RenderInheritance { get; set; }

        public Type ReflectedType { get; private set; }

        public int Level { get; protected set; }

        public KeyedList<ClassMemberDescriptor> Members { get { return _members; } }

        public KeyedList<ClassMethodDescriptor> Methods { get { return _methods; } }

        public TypeMetaModel MetaModel { get; private set; }

        public string Color { get; private set; }

        public override int GetHashCode()
        {
            return this.ReflectedType.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            RootClassDescriptor descriptor = obj as RootClassDescriptor;

            if (descriptor == null)
                return false;

            return descriptor.ReflectedType == this.ReflectedType;
        }

        internal abstract IDescriptorWriter GetWriter(ClassDiagram diagram);
    }
}
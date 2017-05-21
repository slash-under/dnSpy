﻿/*
    Copyright (C) 2014-2017 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using dnSpy.Contracts.Debugger;
using dnSpy.Contracts.Debugger.Evaluation;
using dnSpy.Contracts.Debugger.Evaluation.Engine;

namespace dnSpy.Debugger.Evaluation {
	sealed class DbgValueNodeImpl : DbgValueNode {
		public override DbgLanguage Language { get; }
		public override DbgRuntime Runtime { get; }
		public override DbgValue Value => value;
		public override string Expression => engineValueNode.Expression;
		public override string ImageName => engineValueNode.ImageName;
		public override bool IsReadOnly => engineValueNode.IsReadOnly;
		public override bool CausesSideEffects => engineValueNode.CausesSideEffects;
		public override bool? HasChildren => engineValueNode.HasChildren;
		public override ulong ChildCount => engineValueNode.ChildrenCount;

		readonly DbgEngineValueNode engineValueNode;
		DbgValueImpl value;

		public DbgValueNodeImpl(DbgLanguage language, DbgRuntime runtime, DbgEngineValueNode engineValueNode) {
			Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
			Language = language ?? throw new ArgumentNullException(nameof(language));
			this.engineValueNode = engineValueNode ?? throw new ArgumentNullException(nameof(engineValueNode));
			value = new DbgValueImpl(runtime, engineValueNode.Value);
		}

		public override DbgValueNode[] GetChildren(ulong index, int count) {
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			var engineNodes = engineValueNode.GetChildren(index, count);
			return DbgValueNodeUtils.ToValueNodeArray(Language, Runtime, engineNodes);
		}

		public override void GetChildren(ulong index, int count, Action<DbgValueNode[]> callback) {
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if (callback == null)
				throw new ArgumentNullException(nameof(callback));
			engineValueNode.GetChildren(index, count, engineNodes => callback(DbgValueNodeUtils.ToValueNodeArray(Language, Runtime, engineNodes)));
		}

		public override void Format(IDbgValueNodeFormatParameters options) {
			if (options == null)
				throw new ArgumentNullException(nameof(options));
			engineValueNode.Format(options);
		}

		public override void Format(IDbgValueNodeFormatParameters options, Action callback) {
			if (options == null)
				throw new ArgumentNullException(nameof(options));
			if (callback == null)
				throw new ArgumentNullException(nameof(callback));
			engineValueNode.Format(options, callback);
		}

		DbgValueNodeAssignmentResult OnAssignmentComplete(DbgEngineValueNodeAssignmentResult result) {
			if (result.Error != null) {
				if (engineValueNode.Value != value.EngineValue)
					throw new InvalidOperationException();
				return new DbgValueNodeAssignmentResult(result.Error);
			}
			if (result.Value != engineValueNode.Value)
				throw new InvalidOperationException();
			lock (engineValueNode) {
				var oldValue = value;
				value = new DbgValueImpl(Runtime, result.Value);
				Process.DbgManager.Close(oldValue);
			}
			return new DbgValueNodeAssignmentResult(error: null);
		}

		public override DbgValueNodeAssignmentResult Assign(string expression, DbgEvaluationOptions options) {
			if (expression == null)
				throw new ArgumentNullException(nameof(expression));
			var res = engineValueNode.Assign(expression, options);
			return OnAssignmentComplete(res);
		}

		public override void Assign(string expression, DbgEvaluationOptions options, Action<DbgValueNodeAssignmentResult> callback) {
			if (expression == null)
				throw new ArgumentNullException(nameof(expression));
			if (callback == null)
				throw new ArgumentNullException(nameof(callback));
			engineValueNode.Assign(expression, options, res => callback(OnAssignmentComplete(res)));
		}

		protected override void CloseCore() {
			Value.Close(Process.DbgManager.Dispatcher);
			engineValueNode.Close(Process.DbgManager.Dispatcher);
		}
	}
}

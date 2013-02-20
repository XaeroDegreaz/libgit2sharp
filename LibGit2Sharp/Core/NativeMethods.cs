using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using LibGit2Sharp.Core.Handles;

// ReSharper disable InconsistentNaming

namespace LibGit2Sharp.Core {
	internal static class NativeMethods {
		public const uint GIT_PATH_MAX = 4096;
		private const string libgit2 = "git2";
		private static readonly LibraryLifetimeObject lifetimeObject;

		/// <summary>
		/// Internal hack to ensure that the call to git_threads_shutdown is called after all handle finalizers
		/// have run to completion ensuring that no dangling git-related finalizer runs after git_threads_shutdown.
		/// There should never be more than one instance of this object per AppDomain.
		/// </summary>
		private sealed class LibraryLifetimeObject : CriticalFinalizerObject {
			// Ensure mono can JIT the .cctor and adjust the PATH before trying to load the native library.
			// See https://github.com/libgit2/libgit2sharp/pull/190
			[MethodImpl( MethodImplOptions.NoInlining )]
			public LibraryLifetimeObject() {
				Ensure.ZeroResult( git_threads_init() );
			}

			~LibraryLifetimeObject() {
				git_threads_shutdown();
			}
		}

		static NativeMethods() {
			if ( !IsRunningOnLinux() ) {
				string originalAssemblypath = new Uri( Assembly.GetExecutingAssembly().EscapedCodeBase ).LocalPath;

				string currentArchSubPath = "NativeBinaries/" + ProcessorArchitecture;

				string path = Path.Combine( Path.GetDirectoryName( originalAssemblypath ), currentArchSubPath );

				const string pathEnvVariable = "PATH";
				Environment.SetEnvironmentVariable( pathEnvVariable,
				                                    String.Format( CultureInfo.InvariantCulture, "{0}{1}{2}", path, Path.PathSeparator, Environment.GetEnvironmentVariable( pathEnvVariable ) ) );
			}

			// See LibraryLifetimeObject description.
			lifetimeObject = new LibraryLifetimeObject();
		}

		public static string ProcessorArchitecture {
			get {
				if ( Compat.Environment.Is64BitProcess ) {
					return "amd64";
				}

				return "x86";
			}
		}

		private static bool IsRunningOnLinux() {
			// see http://mono-project.com/FAQ%3a_Technical#Mono_Platforms
			var p = (int)Environment.OSVersion.Platform;
			return ( p == 4 ) || ( p == 6 ) || ( p == 128 );
		}

		[DllImport( "kernel32.dll" )]
		public static extern IntPtr LoadLibrary( [In, MarshalAs( UnmanagedType.LPStr )] string dll = "Assets\\Plugins\\git2.dll" );

		[DllImport( "kernel32.dll", CharSet = CharSet.Ansi )]
		public static extern IntPtr GetProcAddress( IntPtr module, string method );

		[DllImport( "kernel32.dll" )]
		public static extern bool FreeLibrary( IntPtr module );

		private static Delegate getDelegate<T>( out IntPtr module, string method ) {
			module = LoadLibrary();
			IntPtr addr = GetProcAddress( module, method );

			if ( addr == IntPtr.Zero ) {
				throw new EntryPointNotFoundException();
			}

			return Marshal.GetDelegateForFunctionPointer( addr, typeof( T ) );
		}

		private delegate GitErrorSafeHandle d_giterr_last();

		internal static GitErrorSafeHandle giterr_last() {
			IntPtr module;
			var d = (d_giterr_last)getDelegate<d_giterr_last>( out module, "_giterr_last@0" );

			GitErrorSafeHandle result = d();

			FreeLibrary( module );
			return result;
		}

		private delegate void d_giterr_set_str( GitErrorCategory error_class, string errorString );

		internal static void giterr_set_str( GitErrorCategory error_class, string errorString ) {
			IntPtr module;
			( (d_giterr_set_str)getDelegate<d_giterr_set_str>( out module, "_giterr_set_str@8" ) )( error_class, errorString );

			FreeLibrary( module );
		}

		private delegate void d_giterr_set_oom();

		internal static void giterr_set_oom() {
			IntPtr module;
			( (d_giterr_set_oom)getDelegate<d_giterr_set_oom>( out module, "_giterr_set_oom@0" ) )();

			FreeLibrary( module );
		}

		private delegate int d_git_blob_create_fromdisk(
			ref GitOid id,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath path );

		internal static int git_blob_create_fromdisk(ref GitOid id, RepositorySafeHandle repo, FilePath path ) {
			IntPtr module;
			var d = (d_git_blob_create_fromdisk)getDelegate<d_git_blob_create_fromdisk>( out module, "_git_blob_create_fromdisk@12" );

			int result = d( ref id, repo, path );

			FreeLibrary( module );
			return result;
		}

		
		private delegate int d_git_blob_create_fromworkdir(
			ref GitOid id,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath relative_path );

		internal static int git_blob_create_fromworkdir( ref GitOid id, RepositorySafeHandle repo, FilePath relative_path ) {
			IntPtr module;
			var d = (d_git_blob_create_fromworkdir)getDelegate<d_git_blob_create_fromworkdir>( out module, "_git_blob_create_fromworkdir@12" );

			int result = d( ref id, repo, relative_path );

			FreeLibrary( module );
			return result;
		}

		internal delegate int source_callback(
			IntPtr content,
			int max_length,
			IntPtr data );

		private delegate int d_git_blob_create_fromchunks(
			ref GitOid oid,
			RepositorySafeHandle repositoryPtr,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath hintpath,
			source_callback fileCallback,
			IntPtr data );

		internal static int git_blob_create_fromchunks( ref GitOid oid, RepositorySafeHandle repositoryPtr, FilePath hintpath, source_callback fileCallback, IntPtr data ) {
			IntPtr module;
			var d = (d_git_blob_create_fromchunks)getDelegate<d_git_blob_create_fromchunks>( out module, "_git_blob_create_fromchunks@20" );

			int result = d( ref oid, repositoryPtr, hintpath, fileCallback, data );

			FreeLibrary( module );
			return result;
		}

		private delegate IntPtr d_git_blob_rawcontent( GitObjectSafeHandle blob );
		
		internal static IntPtr git_blob_rawcontent( GitObjectSafeHandle blob ) {
			IntPtr module;
			var d = (d_git_blob_rawcontent)getDelegate<d_git_blob_rawcontent>( out module, "_git_blob_rawcontent@4" );

			IntPtr result = d( blob );

			FreeLibrary( module );
			return result;
		}

		private delegate Int64 d_git_blob_rawsize( GitObjectSafeHandle blob );

		internal static Int64 git_blob_rawsize( GitObjectSafeHandle blob ) {
			IntPtr module;
			var d = (d_git_blob_rawsize)getDelegate<d_git_blob_rawsize>( out module, "_git_blob_rawsize@4" );

			Int64 result = d( blob );

			FreeLibrary( module );
			return result;
		}
		
		private delegate int d_git_branch_create(
			out ReferenceSafeHandle ref_out,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string branch_name,
			GitObjectSafeHandle target, // TODO: GitCommitSafeHandle?
			[MarshalAs( UnmanagedType.Bool )] bool force );

		internal static int git_branch_create( out ReferenceSafeHandle ref_out, RepositorySafeHandle repo, string branch_name, GitObjectSafeHandle target, bool force ) {
			IntPtr module;
			var d = (d_git_branch_create)getDelegate<d_git_branch_create>( out module, "_git_branch_create@20" );

			int result = d( out ref_out, repo, branch_name, target, force );

			FreeLibrary( module );
			return result;
		}

		private delegate int d_git_branch_delete( ReferenceSafeHandle reference );

		internal static int git_branch_delete(ReferenceSafeHandle reference ) {
			IntPtr module;
			var d = (d_git_branch_delete)getDelegate<d_git_branch_delete>( out module, "_git_branch_delete@4" );

			int result = d( reference );

			FreeLibrary( module );
			return result;
		}

		internal delegate int branch_foreach_callback(
			IntPtr branch_name,
			GitBranchType branch_type,
			IntPtr payload );

		
		private delegate int d_git_branch_foreach(
			RepositorySafeHandle repo,
			GitBranchType branch_type,
			branch_foreach_callback branch_cb,
			IntPtr payload );

		internal static int git_branch_foreach( RepositorySafeHandle repo, GitBranchType branch_type, branch_foreach_callback branch_cb, IntPtr payload ) {
			IntPtr module;
			var d = (d_git_branch_foreach)getDelegate<d_git_branch_foreach>( out module, "_git_branch_foreach@16" );

			int r = d( repo, branch_type, branch_cb, payload );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_branch_move(
			ReferenceSafeHandle reference,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string new_branch_name,
			[MarshalAs( UnmanagedType.Bool )] bool force );

		internal static int git_branch_move( ReferenceSafeHandle reference, string new_branch_name, bool force ) {
			IntPtr module;
			var d = (d_git_branch_move)getDelegate<d_git_branch_move>( out module, "_git_branch_move@12" );

			int r = d( reference, new_branch_name, force );

			FreeLibrary( module );
			return r;
		}

		//# TODO Figure out where this stuff is inside the native library....
		private delegate int d_git_branch_remote_name(
			byte[] remote_name_out,
			UIntPtr buffer_size,
			RepositorySafeHandle repo,
			ReferenceSafeHandle branch );

		internal static int git_branch_remote_name( byte[] remote_name_out, UIntPtr buffer_size, RepositorySafeHandle repo, ReferenceSafeHandle branch ) {
			//# Can't seem to locate this anywhere in the exports for git2.dll
			//# I don't want the method to load git2.dll, so we return a fake value -- who knows if this method is actually
			//# used at this time?
			return 0;
		}

		private delegate int d_git_branch_tracking_name(
			byte[] tracking_branch_name_out, // NB: This is more properly a StringBuilder, but it's UTF8
			UIntPtr buffer_size,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string referenceName );

		internal static int git_branch_tracking_name( byte[] tracking_branch_name_out, UIntPtr buffer_size, RepositorySafeHandle repo, string referenceName ) {
			IntPtr module;
			var d = (d_git_branch_tracking_name)getDelegate<d_git_branch_tracking_name>( out module, "_git_branch_tracking_name@16" );

			int r = d( tracking_branch_name_out, buffer_size, repo, referenceName );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_checkout_tree(
			RepositorySafeHandle repo,
			GitObjectSafeHandle treeish,
			ref GitCheckoutOpts opts );

		internal static int git_checkout_tree( RepositorySafeHandle repo, GitObjectSafeHandle treeish, ref GitCheckoutOpts opts ) {
			IntPtr module;
			var d = (d_git_checkout_tree)getDelegate<d_git_checkout_tree>( out module, "_git_checkout_tree@12" );

			int r = d( repo, treeish, ref opts );

			FreeLibrary( module );
			return r;
		}
		
		private delegate int d_git_checkout_index(
			RepositorySafeHandle repo,
			GitObjectSafeHandle treeish,
			ref GitCheckoutOpts opts );

		internal static int git_checkout_index( RepositorySafeHandle repo, GitObjectSafeHandle treeish, ref GitCheckoutOpts opts ) {
			IntPtr module;
			var d = (d_git_checkout_index)getDelegate<d_git_checkout_index>( out module, "_git_checkout_index@12" );

			int r = d( repo, treeish, ref opts );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_clone(
			out RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string origin_url,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath workdir_path,
			GitCloneOptions opts );

		internal static int git_clone( out RepositorySafeHandle repo, string origin_url, FilePath workdir_path, GitCloneOptions opts ) {
			IntPtr module;
			var d = (d_git_clone)getDelegate<d_git_clone>( out module, "_git_clone@16" );

			int r = d( out repo, origin_url, workdir_path, opts );

			FreeLibrary( module );
			return r;
		}

		
		public delegate IntPtr d_git_commit_author( GitObjectSafeHandle commit );

		internal static IntPtr git_commit_author( GitObjectSafeHandle commit ) {
			IntPtr module;
			var d = (d_git_commit_author)getDelegate<d_git_commit_author>( out module, "_git_commit_author@4" );

			IntPtr r = d( commit );

			FreeLibrary( module );
			return r;
		}

		public delegate IntPtr d_git_commit_committer( GitObjectSafeHandle commit );

		internal static IntPtr git_commit_committer( GitObjectSafeHandle commit ) {
			IntPtr module;
			var d = (d_git_commit_committer)getDelegate<d_git_commit_committer>( out module, "_git_commit_committer@4" );

			IntPtr r = d( commit );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_commit_create(
			out GitOid id,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string updateRef,
			SignatureSafeHandle author,
			SignatureSafeHandle committer,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string encoding,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string message,
			GitObjectSafeHandle tree,
			int parentCount,
			[MarshalAs( UnmanagedType.LPArray, SizeParamIndex = 7 )] [In] IntPtr[] parents );

		internal static int git_commit_create( out GitOid id, RepositorySafeHandle repo, string updateRef, SignatureSafeHandle author,
		                                       SignatureSafeHandle committer, string encoding, string message, GitObjectSafeHandle tree,
		                                       int parentCount, [In] IntPtr[] parents ) {
			IntPtr module;
			var d = (d_git_commit_create)getDelegate<d_git_commit_create>( out module, "_git_commit_create@40" );

			int r = d( out id, repo, updateRef, author, committer, encoding, message, tree, parentCount, parents );

			FreeLibrary( module );
			return r;
		}
		
		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		private delegate string d_git_commit_message( GitObjectSafeHandle commit );

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		internal static string git_commit_message( GitObjectSafeHandle commit ) {
			IntPtr module;
			var d = (d_git_commit_message)getDelegate<d_git_commit_message>( out module, "_git_commit_message@4" );

			string r = d( commit );

			FreeLibrary( module );
			return r;
		}

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		private delegate string d_git_commit_message_encoding( GitObjectSafeHandle commit );

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		internal static string git_commit_message_encoding( GitObjectSafeHandle commit ) {
			IntPtr module;
			var d = (d_git_commit_message_encoding)getDelegate<d_git_commit_message_encoding>( out module, "_git_commit_message_encoding@4" );

			string r = d( commit );

			FreeLibrary( module );
			return r;
		}
		
		private delegate  OidSafeHandle d_git_commit_parent_id( GitObjectSafeHandle commit, uint n );

		internal static OidSafeHandle git_commit_parent_id( GitObjectSafeHandle commit, uint n ) {
			IntPtr module;
			var d = (d_git_commit_parent_id)getDelegate<d_git_commit_parent_id>( out module, "_git_commit_parent_id@8" );

			OidSafeHandle r = d( commit, n );

			FreeLibrary( module );
			return r;
		}

		private delegate uint d_git_commit_parentcount( GitObjectSafeHandle commit );

		internal static uint git_commit_parentcount( GitObjectSafeHandle commit ) {
			IntPtr module;
			var d = (d_git_commit_parentcount)getDelegate<d_git_commit_parentcount>( out module, "_git_commit_parentcount@4" );

			uint r = d( commit );

			FreeLibrary( module );
			return r;
		}

		private delegate  OidSafeHandle d_git_commit_tree_id( GitObjectSafeHandle commit );

		internal static OidSafeHandle git_commit_tree_id( GitObjectSafeHandle commit ) {
			IntPtr module;
			var d = (d_git_commit_tree_id)getDelegate<d_git_commit_tree_id>( out module, "_git_commit_tree_id@4" );

			OidSafeHandle r = d( commit );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_config_delete_entry( ConfigurationSafeHandle cfg, string name );

		internal static int git_config_delete_entry( ConfigurationSafeHandle cfg, string name ) {
			IntPtr module;
			var d = (d_git_config_delete_entry)getDelegate<d_git_config_delete_entry>( out module, "_git_config_delete_entry@8" );

			int r = d( cfg, name );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_config_find_global( byte[] global_config_path, UIntPtr length );

		internal static int git_config_find_global( byte[] global_config_path, UIntPtr length ) {
			IntPtr module;
			var d = (d_git_config_find_global)getDelegate<d_git_config_find_global>( out module, "_git_config_find_global@8" );

			int r = d( global_config_path, length );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_config_find_system( byte[] system_config_path, UIntPtr length );

		internal static int git_config_find_system( byte[] system_config_path, UIntPtr length ) {
			IntPtr module;
			var d = (d_git_config_find_system)getDelegate<d_git_config_find_system>( out module, "_git_config_find_system@8" );

			int r = d( system_config_path, length );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_config_find_xdg( byte[] xdg_config_path, UIntPtr length );

		internal static int git_config_find_xdg( byte[] xdg_config_path, UIntPtr length ) {
			IntPtr module;
			var d = (d_git_config_find_xdg)getDelegate<d_git_config_find_xdg>( out module, "_git_config_find_xdg@8" );

			int r = d( xdg_config_path, length );

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_config_free( IntPtr cfg );

		internal static void git_config_free( IntPtr cfg ) {
			IntPtr module;
			var d = (d_git_config_free)getDelegate<d_git_config_free>( out module, "_git_config_free@4" );

			d( cfg );

			FreeLibrary( module );
		}

		private delegate int d_git_config_get_entry(
			out GitConfigEntryHandle entry,
			ConfigurationSafeHandle cfg,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string name );

		internal static int git_config_get_entry( out GitConfigEntryHandle entry, ConfigurationSafeHandle cfg, string name ) {
			IntPtr module;
			var d = (d_git_config_get_entry)getDelegate<d_git_config_get_entry>( out module, "_git_config_get_entry@12" );

			int r = d( out entry, cfg, name );

			FreeLibrary( module );
			return r;
		}

		
		private delegate int d_git_config_add_file_ondisk(
			ConfigurationSafeHandle cfg,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath path,
			uint level,
			bool force );

		internal static int git_config_add_file_ondisk( ConfigurationSafeHandle cfg, FilePath path, uint level, bool force ) {
			IntPtr module;
			var d = (d_git_config_add_file_ondisk)getDelegate<d_git_config_add_file_ondisk>( out module, "_git_config_add_file_ondisk@16" );

			int r = d( cfg, path, level, force );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_config_new( out ConfigurationSafeHandle cfg );

		internal static int git_config_new( out ConfigurationSafeHandle cfg ) {
			IntPtr module;
			var d = (d_git_config_new)getDelegate<d_git_config_new>( out module, "_git_config_new@4" );

			int r = d( out cfg );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_config_open_level(
			out ConfigurationSafeHandle cfg,
			ConfigurationSafeHandle parent,
			uint level );

		internal static int git_config_open_level( out ConfigurationSafeHandle cfg, ConfigurationSafeHandle parent, uint level ) {
			IntPtr module;
			var d = (d_git_config_open_level)getDelegate<d_git_config_open_level>( out module, "_git_config_open_level@12" );

			int r = d( out cfg, parent, level );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_config_parse_bool(
			[MarshalAs( UnmanagedType.Bool )] out bool value,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string valueToParse );

		internal static int git_config_parse_bool( out bool value, string valueToParse ) {
			IntPtr module;
			var d = (d_git_config_parse_bool)getDelegate<d_git_config_parse_bool>( out module, "_git_config_parse_bool@8" );

			int r = d( out value, valueToParse );

			FreeLibrary( module );
			return r;
		}
		
		private delegate int d_git_config_parse_int32(
			[MarshalAs( UnmanagedType.I4 )] out int value,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string valueToParse );

		internal static int git_config_parse_int32( out int value, string valueToParse ) {
			IntPtr module;
			var d = (d_git_config_parse_int32)getDelegate<d_git_config_parse_int32>( out module, "_git_config_parse_int32@8" );

			int r = d( out value, valueToParse );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_config_parse_int64(
			[MarshalAs( UnmanagedType.I8 )] out long value,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string valueToParse );

		internal static int git_config_parse_int64( out long value, string valueToParse ) {
			IntPtr module;
			var d = (d_git_config_parse_int64)getDelegate<d_git_config_parse_int64>( out module, "_git_config_parse_int64@8" );

			int r = d( out value, valueToParse );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_config_set_bool(
			ConfigurationSafeHandle cfg,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string name,
			[MarshalAs( UnmanagedType.Bool )] bool value );

		internal static int git_config_set_bool( ConfigurationSafeHandle cfg, string name, bool value ) {
			IntPtr module;
			var d = (d_git_config_set_bool)getDelegate<d_git_config_set_bool>( out module, "_git_config_set_bool@12" );

			int r = d( cfg, name, value );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_config_set_int32(
			ConfigurationSafeHandle cfg,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string name,
			int value );

		internal static int git_config_set_int32( ConfigurationSafeHandle cfg, string name, int value ) {
			IntPtr module;
			var d = (d_git_config_set_int32)getDelegate<d_git_config_set_int32>( out module, "_git_config_set_int32@12" );

			int r = d( cfg, name, value );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_config_set_int64(
			ConfigurationSafeHandle cfg,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string name,
			long value );

		internal static int git_config_set_int64( ConfigurationSafeHandle cfg, string name, long value ) {
			IntPtr module;
			var d = (d_git_config_set_int64)getDelegate<d_git_config_set_int64>( out module, "_git_config_set_int64@16" );

			int r = d( cfg, name, value );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_config_set_string(
			ConfigurationSafeHandle cfg,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string name,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string value );

		internal static int git_config_set_string( ConfigurationSafeHandle cfg, string name, string value ) {
			IntPtr module;
			var d = (d_git_config_set_string)getDelegate<d_git_config_set_string>( out module, "_git_config_set_string@12" );

			int r = d( cfg, name, value );

			FreeLibrary( module );
			return r;
		}

		internal delegate int config_foreach_callback(
			IntPtr entry,
			IntPtr payload );
		
		private delegate int d_git_config_foreach(
			ConfigurationSafeHandle cfg,
			config_foreach_callback callback,
			IntPtr payload );

		internal static int git_config_foreach( ConfigurationSafeHandle cfg, config_foreach_callback callback, IntPtr payload ) {
			IntPtr module;
			var d = (d_git_config_foreach)getDelegate<d_git_config_foreach>( out module, "_git_config_foreach@12" );

			int r = d( cfg, callback, payload );

			FreeLibrary( module );
			return r;
		}

		// Ordinarily we would decorate the `url` parameter with the Utf8Marshaler like we do everywhere
		// else, but apparently doing a native->managed callback with the 64-bit version of CLR 2.0 can
		// sometimes vomit when using a custom IMarshaler.  So yeah, don't do that.  If you need the url,
		// call Utf8Marshaler.FromNative manually.  See the discussion here:
		// http://social.msdn.microsoft.com/Forums/en-US/netfx64bit/thread/1eb746c6-d695-4632-8a9e-16c4fa98d481
		internal delegate int git_cred_acquire_cb(
			out IntPtr cred,
			IntPtr url,
			uint allowed_types,
			IntPtr payload );

		private delegate int d_git_cred_userpass_plaintext_new(
			out IntPtr cred,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string username,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string password );

		internal static int git_cred_userpass_plaintext_new( out IntPtr cred, string username, string password ) {
			IntPtr module;
			var d = (d_git_cred_userpass_plaintext_new)getDelegate<d_git_cred_userpass_plaintext_new>( out module, "_git_cred_userpass_plaintext_new@12" );

			int r = d( out cred, username, password );

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_diff_list_free( IntPtr diff );

		internal static void git_diff_list_free( IntPtr diff ) {
			IntPtr module;
			var d = (d_git_diff_list_free)getDelegate<d_git_diff_list_free>( out module, "_git_diff_list_free@4" );

			d( diff );

			FreeLibrary( module );
		}

		private delegate int d_git_diff_tree_to_tree(
			out DiffListSafeHandle diff,
			RepositorySafeHandle repo,
			GitObjectSafeHandle oldTree,
			GitObjectSafeHandle newTree,
			GitDiffOptions options );

		internal static int git_diff_tree_to_tree( out DiffListSafeHandle diff, RepositorySafeHandle repo,
		                                           GitObjectSafeHandle oldTree, GitObjectSafeHandle newTree, GitDiffOptions options ) {
			IntPtr module;
			var d = (d_git_diff_tree_to_tree)getDelegate<d_git_diff_tree_to_tree>( out module, "_git_diff_tree_to_tree@20" );

			int r = d( out diff, repo, oldTree, newTree, options );

			FreeLibrary( module );
			return r;
		}
		
		private delegate int d_git_diff_tree_to_index(
			out DiffListSafeHandle diff,
			RepositorySafeHandle repo,
			GitObjectSafeHandle oldTree,
			IndexSafeHandle index,
			GitDiffOptions options );

		internal static int git_diff_tree_to_index( out DiffListSafeHandle diff, RepositorySafeHandle repo,
		                                            GitObjectSafeHandle oldTree, IndexSafeHandle index, GitDiffOptions options ) {
			IntPtr module;
			var d = (d_git_diff_tree_to_index)getDelegate<d_git_diff_tree_to_index>( out module, "_git_diff_tree_to_index@20" );

			int r = d( out diff, repo, oldTree, index, options );

			FreeLibrary( module );
			return r;
		}

		
		private delegate int d_git_diff_merge(
			DiffListSafeHandle onto,
			DiffListSafeHandle from );

		internal static int git_diff_merge( DiffListSafeHandle onto, DiffListSafeHandle from ) {
			IntPtr module;
			var d = (d_git_diff_merge)getDelegate<d_git_diff_merge>( out module, "_git_diff_merge@8" );

			int r = d( onto, from );

			FreeLibrary( module );
			return r;
		}
		
		private delegate int d_git_diff_index_to_workdir(
			out DiffListSafeHandle diff,
			RepositorySafeHandle repo,
			IndexSafeHandle index,
			GitDiffOptions options );

		internal static int git_diff_index_to_workdir( out DiffListSafeHandle diff, RepositorySafeHandle repo, IndexSafeHandle index, GitDiffOptions options ) {
			IntPtr module;
			var d = (d_git_diff_index_to_workdir)getDelegate<d_git_diff_index_to_workdir>( out module, "_git_diff_index_to_workdir@16" );

			int r = d( out diff, repo, index, options );

			FreeLibrary( module );
			return r;
		}
		
		private delegate int d_git_diff_tree_to_workdir(
			out DiffListSafeHandle diff,
			RepositorySafeHandle repo,
			GitObjectSafeHandle oldTree,
			GitDiffOptions options );

		internal static int git_diff_tree_to_workdir( out DiffListSafeHandle diff, RepositorySafeHandle repo, GitObjectSafeHandle oldTree, GitDiffOptions options ) {
			IntPtr module;
			var d = (d_git_diff_tree_to_workdir)getDelegate<d_git_diff_tree_to_workdir>( out module, "_git_diff_tree_to_workdir@16" );

			int r = d( out diff, repo, oldTree, options );

			FreeLibrary( module );
			return r;
		}

		internal delegate int git_diff_file_cb(
			GitDiffDelta delta,
			float progress,
			IntPtr payload );

		internal delegate int git_diff_hunk_cb(
			GitDiffDelta delta,
			GitDiffRange range,
			IntPtr header,
			UIntPtr headerLen,
			IntPtr payload );

		internal delegate int git_diff_data_cb(
			GitDiffDelta delta,
			GitDiffRange range,
			GitDiffLineOrigin lineOrigin,
			IntPtr content,
			UIntPtr contentLen,
			IntPtr payload );

		private delegate int d_git_diff_print_patch(
			DiffListSafeHandle diff,
			git_diff_data_cb printCallback,
			IntPtr payload );

		internal static int git_diff_print_patch( DiffListSafeHandle diff, git_diff_data_cb printCallback, IntPtr payload ) {
			IntPtr module;
			var d = (d_git_diff_print_patch)getDelegate<d_git_diff_print_patch>( out module, "_git_diff_print_patch@12" );

			int r = d( diff, printCallback, payload );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_diff_blobs(
			GitObjectSafeHandle oldBlob,
			GitObjectSafeHandle newBlob,
			GitDiffOptions options,
			git_diff_file_cb fileCallback,
			git_diff_hunk_cb hunkCallback,
			git_diff_data_cb lineCallback,
			IntPtr payload );

		internal static int git_diff_blobs( GitObjectSafeHandle oldBlob, GitObjectSafeHandle newBlob, GitDiffOptions options,
		                                    git_diff_file_cb fileCallback, git_diff_hunk_cb hunkCallback,
		                                    git_diff_data_cb lineCallback, IntPtr payload ) {
			IntPtr module;
			var d = (d_git_diff_blobs)getDelegate<d_git_diff_blobs>( out module, "_git_diff_blobs@28" );

			int r = d( oldBlob, newBlob, options, fileCallback, hunkCallback, lineCallback, payload );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_graph_ahead_behind( 
			out UIntPtr ahead, 
			out UIntPtr behind, 
			RepositorySafeHandle repo, 
			ref GitOid one, 
			ref GitOid two );

		internal static int git_graph_ahead_behind( out UIntPtr ahead, out UIntPtr behind, RepositorySafeHandle repo, ref GitOid one, ref GitOid two ) {
			IntPtr module;
			var d = (d_git_graph_ahead_behind)getDelegate<d_git_graph_ahead_behind>( out module, "_git_graph_ahead_behind@20" );

			int r = d( out ahead, out behind, repo, ref one, ref two );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_ignore_add_rule(
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string rules );

		internal static int git_ignore_add_rule( RepositorySafeHandle repo, string rules ) {
			IntPtr module;
			var d = (d_git_ignore_add_rule)getDelegate<d_git_ignore_add_rule>( out module, "_git_ignore_add_rule@8" );

			int r = d( repo, rules );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_ignore_clear_internal_rules( RepositorySafeHandle repo );

		internal static int git_ignore_clear_internal_rules( RepositorySafeHandle repo ) {
			IntPtr module;
			var d = (d_git_ignore_clear_internal_rules)getDelegate<d_git_ignore_clear_internal_rules>( out module, "_git_ignore_clear_internal_rules@4" );

			int r = d( repo );

			FreeLibrary( module );
			return r;
		}
		
		private delegate int d_git_ignore_path_is_ignored(
			out int ignored,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string path );

		internal static int git_ignore_path_is_ignored( out int ignored, RepositorySafeHandle repo, string path ) {
			IntPtr module;
			var d = (d_git_ignore_path_is_ignored)getDelegate<d_git_ignore_path_is_ignored>( out module, "_git_ignore_path_is_ignored@12" );

			int r = d( out ignored, repo, path );

			FreeLibrary( module );
			return r;
		}
		
		private delegate int d_git_index_add_bypath(
			IndexSafeHandle index,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath path );

		internal static int git_index_add_bypath( IndexSafeHandle index, FilePath path ) {
			IntPtr module;
			var d = (d_git_index_add_bypath)getDelegate<d_git_index_add_bypath>( out module, "_git_index_add_bypath@8" );

			int r = d( index, path );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_index_add(
			IndexSafeHandle index,
			GitIndexEntry entry );

		internal static int git_index_add( IndexSafeHandle index, GitIndexEntry entry ) {
			IntPtr module;
			var d = (d_git_index_add)getDelegate<d_git_index_add>( out module, "_git_index_add@8" );

			int r = d( index, entry );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_index_conflict_get(
			out IndexEntrySafeHandle ancestor,
			out IndexEntrySafeHandle ours,
			out IndexEntrySafeHandle theirs,
			IndexSafeHandle index,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath path );

		internal static int git_index_conflict_get( out IndexEntrySafeHandle ancestor, out IndexEntrySafeHandle ours,
		                                            out IndexEntrySafeHandle theirs, IndexSafeHandle index, FilePath path ) {
			IntPtr module;
			var d = (d_git_index_conflict_get)getDelegate<d_git_index_conflict_get>( out module, "_git_index_conflict_get@20" );

			int r = d( out ancestor, out ours, out theirs, index, path );

			FreeLibrary( module );
			return r;
		}

		private delegate UIntPtr d_git_index_entrycount( IndexSafeHandle index );

		internal static UIntPtr git_index_entrycount( IndexSafeHandle index ) {
			IntPtr module;
			var d = (d_git_index_entrycount)getDelegate<d_git_index_entrycount>( out module, "_git_index_entrycount@4" );

			UIntPtr r = d( index );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_index_entry_stage( IndexEntrySafeHandle indexentry );

		internal static int git_index_entry_stage( IndexEntrySafeHandle indexentry ) {
			IntPtr module;
			var d = (d_git_index_entry_stage)getDelegate<d_git_index_entry_stage>( out module, "_git_index_entry_stage@4" );

			int r = d( indexentry );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_index_find(
			out UIntPtr pos,
			IndexSafeHandle index,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath path );

		internal static int git_index_find( out UIntPtr pos, IndexSafeHandle index, FilePath path ) {
			IntPtr module;
			var d = (d_git_index_find)getDelegate<d_git_index_find>( out module, "_git_index_find@12" );

			int r = d( out pos, index, path );

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_index_free( IntPtr index );

		internal static void git_index_free( IntPtr index ) {
			IntPtr module;
			var d = (d_git_index_free)getDelegate<d_git_index_free>( out module, "_git_index_free@4" );

			d( index );

			FreeLibrary( module );
		}

		
		private delegate IndexEntrySafeHandle d_git_index_get_byindex( 
			IndexSafeHandle index, 
			UIntPtr n );

		internal static IndexEntrySafeHandle git_index_get_byindex( IndexSafeHandle index, UIntPtr n ) {
			IntPtr module;
			var d = (d_git_index_get_byindex)getDelegate<d_git_index_get_byindex>( out module, "_git_index_get_byindex@8" );

			IndexEntrySafeHandle r = d( index, n );

			FreeLibrary( module );
			return r;
		}

		private delegate IndexEntrySafeHandle d_git_index_get_bypath(
			IndexSafeHandle index,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath path,
			int stage );

		internal static IndexEntrySafeHandle git_index_get_bypath( IndexSafeHandle index, FilePath path, int stage ) {
			IntPtr module;
			var d = (d_git_index_get_bypath)getDelegate<d_git_index_get_bypath>( out module, "_git_index_get_bypath@12" );

			IndexEntrySafeHandle r = d( index, path, stage );

			FreeLibrary( module );
			return r;
		}

		
		private delegate int d_git_index_has_conflicts( IndexSafeHandle index );

		internal static int git_index_has_conflicts( IndexSafeHandle index ) {
			IntPtr module;
			var d = (d_git_index_has_conflicts)getDelegate<d_git_index_has_conflicts>( out module, "_git_index_has_conflicts@4" );

			var r = d( index );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_index_open(
			out IndexSafeHandle index,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath indexpath );

		internal static int git_index_open( out IndexSafeHandle index, FilePath indexpath ) {
			IntPtr module;
			var d = (d_git_index_open)getDelegate<d_git_index_open>( out module, "_git_index_open@8" );

			var r = d( out index, indexpath );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_index_remove(
			IndexSafeHandle index,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath path,
			int stage );

		internal static int git_index_remove( IndexSafeHandle index, FilePath path, int stage ) {
			IntPtr module;
			var d = (d_git_index_remove)getDelegate<d_git_index_remove>( out module, "_git_index_remove@12" );

			var r = d( index, path, stage );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_index_write( IndexSafeHandle index );

		internal static int git_index_write( IndexSafeHandle index ) {
			IntPtr module;
			var d = (d_git_index_write)getDelegate<d_git_index_write>( out module, "_git_index_write@4" );

			var r = d( index );

			FreeLibrary( module );
			return r;
		}

		
		private delegate int d_git_index_write_tree( 
			out GitOid treeOid, 
			IndexSafeHandle index );

		internal static int git_index_write_tree( out GitOid treeOid, IndexSafeHandle index ) {
			IntPtr module;
			var d = (d_git_index_write_tree)getDelegate<d_git_index_write_tree>( out module, "_git_index_write_tree@8" );

			var r = d( out treeOid, index );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_merge_base(
			out GitOid mergeBase,
			RepositorySafeHandle repo,
			GitObjectSafeHandle one,
			GitObjectSafeHandle two );

		internal static int git_merge_base( out GitOid mergeBase, RepositorySafeHandle repo, GitObjectSafeHandle one, GitObjectSafeHandle two ) {
			IntPtr module;
			var d = (d_git_merge_base)getDelegate<d_git_merge_base>( out module, "_git_merge_base@16" );

			var r = d( out mergeBase, repo, one, two );

			FreeLibrary( module );
			return r;
		}
		
		private delegate int d_git_message_prettify(
			byte[] message_out, // NB: This is more properly a StringBuilder, but it's UTF8
			UIntPtr buffer_size,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string message,
			bool strip_comments );

		internal static int git_message_prettify(byte[] message_out, UIntPtr buffer_size,string message,bool strip_comments ) {
			IntPtr module;
			var d = (d_git_message_prettify)getDelegate<d_git_message_prettify>( out module, "_git_message_prettify@16" );

			var r = d( message_out, buffer_size, message, strip_comments );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_note_create(
			out GitOid noteOid,
			RepositorySafeHandle repo,
			SignatureSafeHandle author,
			SignatureSafeHandle committer,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string notes_ref,
			ref GitOid oid,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string note,
			int force );

		internal static int git_note_create( out GitOid noteOid, RepositorySafeHandle repo, SignatureSafeHandle author,
		                                     SignatureSafeHandle committer, string notes_ref, ref GitOid oid,
		                                     string note, int force ) {
			IntPtr module;
			var d = (d_git_note_create)getDelegate<d_git_note_create>( out module, "_git_note_create@32" );

			var r = d( out noteOid, repo, author, committer, notes_ref, ref oid, note, force );

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_note_free( IntPtr note );

		internal static void git_note_free( IntPtr note ) {
			IntPtr module;
			var d = (d_git_note_free)getDelegate<d_git_note_free>( out module, "_git_note_free@4" );

			d( note );

			FreeLibrary( module );
		}

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		private delegate string d_git_note_message( NoteSafeHandle note );

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		internal static string git_note_message( NoteSafeHandle note ) {
			IntPtr module;
			var d = (d_git_note_message)getDelegate<d_git_note_message>( out module, "_git_note_message@4" );

			var r = d( note );

			FreeLibrary( module );
			return r;
		}
		
		private delegate OidSafeHandle g_git_note_oid( NoteSafeHandle note );

		internal static OidSafeHandle git_note_oid( NoteSafeHandle note ) {
			IntPtr module;
			var d = (g_git_note_oid)getDelegate<g_git_note_oid>( out module, "_git_note_oid@4" );

			var r = d( note );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_note_read(
			out NoteSafeHandle note,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string notes_ref,
			ref GitOid oid );

		internal static int git_note_read( out NoteSafeHandle note, RepositorySafeHandle repo, string notes_ref, ref GitOid oid ) {
			IntPtr module;
			var d = (d_git_note_read)getDelegate<d_git_note_read>( out module, "_git_note_read@16" );

			var r = d( out note, repo, notes_ref, ref oid );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_note_remove(
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string notes_ref,
			SignatureSafeHandle author,
			SignatureSafeHandle committer,
			ref GitOid oid );

		internal static int git_note_remove( RepositorySafeHandle repo, string notes_ref, SignatureSafeHandle author,
		                                     SignatureSafeHandle committer, ref GitOid oid ) {
			IntPtr module;
			var d = (d_git_note_remove)getDelegate<d_git_note_remove>( out module, "_git_note_remove@20" );

			var r = d( repo, notes_ref, author, committer, ref oid );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_note_default_ref(
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )] out string notes_ref,
			RepositorySafeHandle repo );

		internal static int git_note_default_ref( out string notes_ref, RepositorySafeHandle repo ) {
			IntPtr module;
			var d = (d_git_note_default_ref)getDelegate<d_git_note_default_ref>( out module, "_git_note_default_ref@8" );

			var r = d( out notes_ref, repo );

			FreeLibrary( module );
			return r;
		}

		internal delegate int git_note_foreach_cb(
			ref GitOid blob_id,
			ref GitOid annotated_object_id,
			IntPtr payload );
		
		private delegate int d_git_note_foreach(
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string notes_ref,
			git_note_foreach_cb cb,
			IntPtr payload );

		internal static int git_note_foreach( RepositorySafeHandle repo, string notes_ref, git_note_foreach_cb cb, IntPtr payload ) {
			IntPtr module;
			var d = (d_git_note_foreach)getDelegate<d_git_note_foreach>( out module, "_git_note_foreach@16" );

			var r = d( repo, notes_ref, cb, payload );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_odb_add_backend( 
			ObjectDatabaseSafeHandle odb, 
			IntPtr backend, 
			int priority );

		internal static int git_odb_add_backend( ObjectDatabaseSafeHandle odb, IntPtr backend, int priority ) {
			IntPtr module;
			var d = (d_git_odb_add_backend)getDelegate<d_git_odb_add_backend>( out module, "_git_odb_add_backend@12" );

			var r = d( odb, backend, priority );

			FreeLibrary( module );
			return r;
		}

		private delegate IntPtr d_git_odb_backend_malloc( IntPtr backend, UIntPtr len );

		internal static IntPtr git_odb_backend_malloc( IntPtr backend, UIntPtr len ) {
			IntPtr module;
			var d = (d_git_odb_backend_malloc)getDelegate<d_git_odb_backend_malloc>( out module, "_git_odb_backend_malloc@8" );

			var r = d( backend, len );

			FreeLibrary( module );
			return r;
		}

	
		private delegate int d_git_odb_exists( ObjectDatabaseSafeHandle odb, ref GitOid id );

		internal static int git_odb_exists( ObjectDatabaseSafeHandle odb, ref GitOid id ) {
			IntPtr module;
			var d = (d_git_odb_exists)getDelegate<d_git_odb_exists>( out module, "_git_odb_exists@8" );

			var r = d( odb, ref id );

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_odb_free( IntPtr odb );

		internal static void git_odb_free( IntPtr odb ) {
			IntPtr module;
			var d = (d_git_odb_free)getDelegate<d_git_odb_free>( out module, "_git_odb_free@4" );

			d( odb );

			FreeLibrary( module );
		}

		private delegate void d_git_object_free( IntPtr obj );

		internal static void git_object_free( IntPtr obj ) {
			IntPtr module;
			var d = (d_git_object_free)getDelegate<d_git_object_free>( out module, "_git_object_free@4" );

			d( obj );

			FreeLibrary( module );
		}

		private delegate OidSafeHandle d_git_object_id( GitObjectSafeHandle obj );

		internal static OidSafeHandle git_object_id( GitObjectSafeHandle obj ) {
			IntPtr module;
			var d = (d_git_object_id)getDelegate<d_git_object_id>( out module, "_git_object_id@4" );

			var r = d( obj );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_object_lookup( 
			out GitObjectSafeHandle obj, 
			RepositorySafeHandle repo, 
			ref GitOid id, 
			GitObjectType type );

		internal static int git_object_lookup( out GitObjectSafeHandle obj, RepositorySafeHandle repo, ref GitOid id, GitObjectType type ) {
			IntPtr module;
			var d = (d_git_object_lookup)getDelegate<d_git_object_lookup>( out module, "_git_object_lookup@16" );

			var r = d( out obj, repo, ref id, type );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_object_peel(
			out GitObjectSafeHandle peeled,
			GitObjectSafeHandle obj,
			GitObjectType type );

		internal static int git_object_peel( out GitObjectSafeHandle peeled, GitObjectSafeHandle obj, GitObjectType type ) {
			IntPtr module;
			var d = (d_git_object_peel)getDelegate<d_git_object_peel>( out module, "_git_object_peel@12" );

			var r = d( out peeled, obj, type );

			FreeLibrary( module );
			return r;
		}
		
		private delegate GitObjectType d_git_object_type( GitObjectSafeHandle obj );

		internal static GitObjectType git_object_type( GitObjectSafeHandle obj ) {
			IntPtr module;
			var d = (d_git_object_type)getDelegate<d_git_object_type>( out module, "_git_object_type@4" );

			var r = d( obj );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_push_new( 
			out PushSafeHandle push, 
			RemoteSafeHandle remote );

		internal static int git_push_new( out PushSafeHandle push, RemoteSafeHandle remote ) {
			IntPtr module;
			var d = (d_git_push_new)getDelegate<d_git_push_new>( out module, "_git_push_new@8" );

			var r = d( out push, remote );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_push_add_refspec(
			PushSafeHandle push,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string pushRefSpec );

		internal static int git_push_add_refspec( PushSafeHandle push, string pushRefSpec ) {
			IntPtr module;
			var d = (d_git_push_add_refspec)getDelegate<d_git_push_add_refspec>( out module, "_git_push_add_refspec@8" );

			var r = d( push, pushRefSpec );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_push_finish( PushSafeHandle push );

		internal static  int git_push_finish( PushSafeHandle push ) {
			IntPtr module;
			var d = (d_git_push_finish)getDelegate<d_git_push_finish>( out module, "_git_push_finish@4" );

			var r = d( push );

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_push_free( IntPtr push );

		internal static void git_push_free( IntPtr push ) {
			IntPtr module;
			var d = (d_git_push_free)getDelegate<d_git_push_free>( out module, "_git_push_free@4" );

			d( push );

			FreeLibrary( module );
		}

		private delegate int d_git_push_status_foreach(
			PushSafeHandle push,
			push_status_foreach_cb status_cb,
			IntPtr data );

		internal static int git_push_status_foreach( PushSafeHandle push, push_status_foreach_cb status_cb, IntPtr data ) {
			IntPtr module;
			var d = (d_git_push_status_foreach)getDelegate<d_git_push_status_foreach>( out module, "_git_push_status_foreach@12" );

			var r = d( push, status_cb, data );

			FreeLibrary( module );
			return r;
		}

		internal delegate int push_status_foreach_cb(
			IntPtr reference,
			IntPtr msg,
			IntPtr data );

		private delegate int d_git_push_unpack_ok( PushSafeHandle push );

		internal static int git_push_unpack_ok( PushSafeHandle push ) {
			IntPtr module;
			var d = (d_git_push_unpack_ok)getDelegate<d_git_push_unpack_ok>( out module, "_git_push_unpack_ok@4" );

			var r = d(push);

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_push_update_tips( PushSafeHandle push );

		internal static int git_push_update_tips( PushSafeHandle push ) {
			IntPtr module;
			var d = (d_git_push_update_tips)getDelegate<d_git_push_update_tips>( out module, "_git_push_update_tips@4" );

			var r = d( push );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_reference_create(
			out ReferenceSafeHandle reference,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string name,
			ref GitOid oid,
			[MarshalAs( UnmanagedType.Bool )] bool force );

		internal static int git_reference_create( out ReferenceSafeHandle reference, RepositorySafeHandle repo, string name,
		                                          ref GitOid oid, bool force ) {
			IntPtr module;
			var d = (d_git_reference_create)getDelegate<d_git_reference_create>( out module, "_git_reference_create@20" );

			var r = d( out reference, repo, name, ref oid, force );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_reference_symbolic_create(
			out ReferenceSafeHandle reference,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string name,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string target,
			[MarshalAs( UnmanagedType.Bool )] bool force );

		internal static int git_reference_symbolic_create( out ReferenceSafeHandle reference, RepositorySafeHandle repo, string name,
		                                                   string target, bool force ) {
			IntPtr module;
			var d = (d_git_reference_symbolic_create)getDelegate<d_git_reference_symbolic_create>( out module, "_git_reference_symbolic_create@20" );

			var r = d( out reference, repo, name, target, force );

			FreeLibrary( module );
			return r;
		}


		private delegate int d_git_reference_delete( ReferenceSafeHandle reference );

		internal static int git_reference_delete( ReferenceSafeHandle reference ) {
			IntPtr module;
			var d = (d_git_reference_delete)getDelegate<d_git_reference_delete>( out module, "_git_reference_delete@4" );

			var r = d( reference );

			FreeLibrary( module );
			return r;
		}

		internal delegate int ref_glob_callback(
			IntPtr reference_name,
			IntPtr payload );

		private delegate int d_git_reference_foreach_glob(
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string glob,
			GitReferenceType flags,
			ref_glob_callback callback,
			IntPtr payload );

		internal static int git_reference_foreach_glob( RepositorySafeHandle repo, string glob, GitReferenceType flags,
		                                                ref_glob_callback callback, IntPtr payload ) {
			IntPtr module;
			var d = (d_git_reference_foreach_glob)getDelegate<d_git_reference_foreach_glob>( out module, "_git_reference_foreach_glob@20" );

			var r = d( repo, glob, flags, callback, payload );

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_reference_free( IntPtr reference );

		internal static void git_reference_free( IntPtr reference ) {
			IntPtr module;
			var d = (d_git_reference_free)getDelegate<d_git_reference_free>( out module, "_git_reference_free@4" );

			d( reference );

			FreeLibrary( module );
		}

		private delegate int d_git_reference_is_valid_name(
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string refname );

		internal static int git_reference_is_valid_name( string refname ) {
			IntPtr module;
			var d = (d_git_reference_is_valid_name)getDelegate<d_git_reference_is_valid_name>( out module, "_git_reference_is_valid_name@4" );

			var r = d( refname );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_reference_lookup(
			out ReferenceSafeHandle reference,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string name );

		internal static int git_reference_lookup( out ReferenceSafeHandle reference, RepositorySafeHandle repo, string name ) {
			IntPtr module;
			var d = (d_git_reference_lookup)getDelegate<d_git_reference_lookup>( out module, "_git_reference_lookup@12" );

			var r = d( out reference, repo, name );

			FreeLibrary( module );
			return r;
		}

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		private delegate string d_git_reference_name( ReferenceSafeHandle reference );

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		internal static string git_reference_name( ReferenceSafeHandle reference ) {
			IntPtr module;
			var d = (d_git_reference_name)getDelegate<d_git_reference_name>( out module, "_git_reference_name@4" );

			var r = d( reference );

			FreeLibrary( module );
			return r;
		}

		private delegate OidSafeHandle d_git_reference_target( ReferenceSafeHandle reference );

		internal static OidSafeHandle git_reference_target( ReferenceSafeHandle reference ) {
			IntPtr module;
			var d = (d_git_reference_target)getDelegate<d_git_reference_target>( out module, "_git_reference_target@4" );

			var r = d( reference );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_reference_rename(
			ReferenceSafeHandle reference,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string newName,
			[MarshalAs( UnmanagedType.Bool )] bool force );

		internal static int git_reference_rename( ReferenceSafeHandle reference, string newName, bool force ) {
			IntPtr module;
			var d = (d_git_reference_rename)getDelegate<d_git_reference_rename>( out module, "_git_reference_rename@12" );

			var r = d( reference, newName, force );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_reference_resolve( 
			out ReferenceSafeHandle resolvedReference, 
			ReferenceSafeHandle reference );

		internal static int git_reference_resolve( out ReferenceSafeHandle resolvedReference, ReferenceSafeHandle reference ) {
			IntPtr module;
			var d = (d_git_reference_resolve)getDelegate<d_git_reference_resolve>( out module, "_git_reference_resolve@8" );

			var r = d( out resolvedReference, reference );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_reference_set_target( 
			ReferenceSafeHandle reference, 
			ref GitOid id );

		internal static int git_reference_set_target( ReferenceSafeHandle reference, ref GitOid id ) {
			IntPtr module;
			var d = (d_git_reference_set_target)getDelegate<d_git_reference_set_target>( out module, "_git_reference_set_target@8" );

			var r = d( reference, ref id );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_reference_symbolic_set_target(
			ReferenceSafeHandle reference,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string target );

		internal static int git_reference_symbolic_set_target( ReferenceSafeHandle reference, string target ) {
			IntPtr module;
			var d = (d_git_reference_symbolic_set_target)getDelegate<d_git_reference_symbolic_set_target>( out module, "_git_reference_symbolic_set_target@8" );

			var r = d( reference, target );

			FreeLibrary( module );
			return r;
		}

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		private delegate string d_git_reference_symbolic_target( ReferenceSafeHandle reference );

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		internal static string git_reference_symbolic_target( ReferenceSafeHandle reference ) {
			IntPtr module;
			var d = (d_git_reference_symbolic_target)getDelegate<d_git_reference_symbolic_target>( out module, "_git_reference_symbolic_target@4" );

			var r = d( reference );

			FreeLibrary( module );
			return r;
		}

		private delegate GitReferenceType d_git_reference_type( ReferenceSafeHandle reference );

		internal static GitReferenceType git_reference_type( ReferenceSafeHandle reference ) {
			IntPtr module;
			var d = (d_git_reference_type)getDelegate<d_git_reference_type>( out module, "_git_reference_type@4" );

			var r = d( reference );

			FreeLibrary( module );
			return r;
		}

		//# TODO: Figure out where this is hiding in git2.dll
		private delegate int d_git_refspec_rtransform(
			byte[] target,
			UIntPtr outlen,
			GitFetchSpecHandle refSpec,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string name );

		internal static int git_refspec_rtransform( byte[] target, UIntPtr outlen, GitFetchSpecHandle refSpec, string name ) {
			var r = 1;
			return r;
		}

		private delegate int d_git_remote_connect( RemoteSafeHandle remote, GitDirection direction );

		internal static int git_remote_connect( RemoteSafeHandle remote, GitDirection direction ) {
			IntPtr module;
			var d = (d_git_remote_connect)getDelegate<d_git_remote_connect>( out module, "_git_remote_connect@8" );

			var r = d( remote, direction );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_remote_create(
			out RemoteSafeHandle remote,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string name,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string url );

		internal static int git_remote_create(out RemoteSafeHandle remote,RepositorySafeHandle repo,string name,string url ) {
			IntPtr module;
			var d = (d_git_remote_create)getDelegate<d_git_remote_create>( out module, "_git_remote_create@16" );

			var r = d( out remote, repo, name, url );

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_remote_disconnect( RemoteSafeHandle remote );

		internal static void git_remote_disconnect( RemoteSafeHandle remote ) {
			IntPtr module;
			var d = (d_git_remote_disconnect)getDelegate<d_git_remote_disconnect>( out module, "_git_remote_disconnect@4" );

			d( remote );

			FreeLibrary( module );
		}

		private delegate int d_git_remote_download(
			RemoteSafeHandle remote,
			git_transfer_progress_callback progress_cb,
			IntPtr payload );

		internal static int git_remote_download( RemoteSafeHandle remote, git_transfer_progress_callback progress_cb, IntPtr payload ) {
			IntPtr module;
			var d = (d_git_remote_download)getDelegate<d_git_remote_download>( out module, "_git_remote_download@12" );

			var r = d( remote, progress_cb, payload );

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_remote_free( IntPtr remote );

		internal static void git_remote_free( IntPtr remote ) {
			IntPtr module;
			var d = (d_git_remote_free)getDelegate<d_git_remote_free>( out module, "_git_remote_free@4" );

			d( remote );

			FreeLibrary( module );
		}

		//# TODO: Locate this in git2.dll
		private delegate int d_git_remote_is_valid_name(
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string remote_name );

		internal static int git_remote_is_valid_name(
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string remote_name ) {
			return 1;
		}

		private delegate int d_git_remote_load(
			out RemoteSafeHandle remote,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string name );

		internal static int git_remote_load( out RemoteSafeHandle remote, RepositorySafeHandle repo, string name ) {
			IntPtr module;
			var d = (d_git_remote_load)getDelegate<d_git_remote_load>( out module, "_git_remote_load@12" );

			var r = d( out remote, repo, name );

			FreeLibrary( module );
			return r;
		}

		internal delegate int git_headlist_cb( ref GitRemoteHead remoteHeadPtr, IntPtr payload );

		private delegate int d_git_remote_ls( 
			RemoteSafeHandle remote, 
			git_headlist_cb headlist_cb, 
			IntPtr payload );

		internal static int git_remote_ls( RemoteSafeHandle remote, git_headlist_cb headlist_cb, IntPtr payload ) {
			IntPtr module;
			var d = (d_git_remote_ls)getDelegate<d_git_remote_ls>( out module, "_git_remote_ls@12" );

			var r = d( remote, headlist_cb, payload );

			FreeLibrary( module );
			return r;
		}

		private delegate GitFetchSpecHandle d_git_remote_fetchspec( RemoteSafeHandle remote );

		internal static GitFetchSpecHandle git_remote_fetchspec( RemoteSafeHandle remote ) {
			IntPtr module;
			var d = (d_git_remote_fetchspec)getDelegate<d_git_remote_fetchspec>( out module, "_git_remote_fetchspec@4" );

			var r = d( remote );

			FreeLibrary( module );
			return r;
		}

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		private delegate string d_git_remote_name( RemoteSafeHandle remote );

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		internal static string git_remote_name( RemoteSafeHandle remote ) {
			IntPtr module;
			var d = (d_git_remote_name)getDelegate<d_git_remote_name>( out module, "_git_remote_name@4" );

			var r = d( remote );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_remote_save( RemoteSafeHandle remote );

		internal static int git_remote_save( RemoteSafeHandle remote ) {
			IntPtr module;
			var d = (d_git_remote_save)getDelegate<d_git_remote_save>( out module, "_git_remote_save@4" );

			var r = d( remote );

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_remote_set_cred_acquire_cb(
			RemoteSafeHandle remote,
			git_cred_acquire_cb cred_acquire_cb,
			IntPtr payload );

		internal static void git_remote_set_cred_acquire_cb( RemoteSafeHandle remote, git_cred_acquire_cb cred_acquire_cb, IntPtr payload ) {
			IntPtr module;
			var d = (d_git_remote_set_cred_acquire_cb)getDelegate<d_git_remote_set_cred_acquire_cb>( out module, "_git_remote_set_cred_acquire_cb@12" );

			d( remote, cred_acquire_cb, payload );

			FreeLibrary( module );
		}

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		private delegate string d_git_remote_url( RemoteSafeHandle remote );

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		internal static string git_remote_url( RemoteSafeHandle remote ) {
			IntPtr module;
			var d = (d_git_remote_url)getDelegate<d_git_remote_url>( out module, "_git_remote_url@4" );

			var r = d( remote );

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_remote_set_autotag( RemoteSafeHandle remote, TagFetchMode option );

		internal static void git_remote_set_autotag( RemoteSafeHandle remote, TagFetchMode option ) {
			IntPtr module;
			var d = (d_git_remote_set_autotag)getDelegate<d_git_remote_set_autotag>( out module, "_git_remote_set_autotag@8" );

			d( remote, option );

			FreeLibrary( module );
		}

		private delegate int d_git_remote_set_callbacks(
			RemoteSafeHandle remote,
			ref GitRemoteCallbacks callbacks );

		internal static int git_remote_set_callbacks(RemoteSafeHandle remote,ref GitRemoteCallbacks callbacks ) {
			IntPtr module;
			var d = (d_git_remote_set_callbacks)getDelegate<d_git_remote_set_callbacks>( out module, "_git_remote_set_callbacks@8" );

			var r = d( remote, ref callbacks );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_remote_set_fetchspec(
			RemoteSafeHandle remote,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string fetchrefspec );

		internal static int git_remote_set_fetchspec( RemoteSafeHandle remote, string fetchrefspec ) {
			IntPtr module;
			var d = (d_git_remote_set_fetchspec)getDelegate<d_git_remote_set_fetchspec>( out module, "_git_remote_set_fetchspec@8" );

			var r = d( remote, fetchrefspec );

			FreeLibrary( module );
			return r;
		}

		internal delegate void remote_progress_callback( IntPtr str, int len, IntPtr data );

		internal delegate int remote_completion_callback( RemoteCompletionType type, IntPtr data );

		internal delegate int remote_update_tips_callback(
			IntPtr refName,
			ref GitOid oldId,
			ref GitOid newId,
			IntPtr data );

		private delegate int d_git_remote_update_tips( RemoteSafeHandle remote );

		internal static int git_remote_update_tips( RemoteSafeHandle remote ) {
			IntPtr module;
			var d = (d_git_remote_update_tips)getDelegate<d_git_remote_update_tips>( out module, "_git_remote_update_tips@4" );

			var r = d( remote );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_repository_discover(
			byte[] repository_path, // NB: This is more properly a StringBuilder, but it's UTF8
			UIntPtr size,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath start_path,
			[MarshalAs( UnmanagedType.Bool )] bool across_fs,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath ceiling_dirs );

		internal static int git_repository_discover( byte[] repository_path, UIntPtr size, FilePath start_path, bool across_fs, FilePath ceiling_dirs ) {
			IntPtr module;
			var d = (d_git_repository_discover)getDelegate<d_git_repository_discover>( out module, "_git_repository_discover@20" );

			var r = d( repository_path, size, start_path, across_fs, ceiling_dirs );

			FreeLibrary( module );
			return r;
		}

		internal delegate int git_repository_fetchhead_foreach_cb(
			IntPtr remote_name,
			IntPtr remote_url,
			ref GitOid oid,
			[MarshalAs( UnmanagedType.Bool )] bool is_merge,
			IntPtr payload );

		private delegate int d_git_repository_fetchhead_foreach(
			RepositorySafeHandle repo,
			git_repository_fetchhead_foreach_cb cb,
			IntPtr payload );

		internal static int git_repository_fetchhead_foreach( RepositorySafeHandle repo, git_repository_fetchhead_foreach_cb cb, IntPtr payload ) {
			IntPtr module;
			var d = (d_git_repository_fetchhead_foreach)getDelegate<d_git_repository_fetchhead_foreach>( out module, "_git_repository_fetchhead_foreach@12" );

			var r = d( repo, cb, payload );

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_repository_free( IntPtr repo );

		internal static void git_repository_free( IntPtr repo ) {
			IntPtr module;
			var d = (d_git_repository_free)getDelegate<d_git_repository_free>( out module, "_git_repository_free@4" );

			d( repo );

			FreeLibrary( module );
		}

		private delegate int d_git_repository_head_detached( RepositorySafeHandle repo );

		internal static int git_repository_head_detached( RepositorySafeHandle repo ) {
			IntPtr module;
			var d = (d_git_repository_head_detached)getDelegate<d_git_repository_head_detached>( out module, "_git_repository_head_detached@4" );

			var r = d( repo );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_repository_head_orphan( RepositorySafeHandle repo );

		internal static int git_repository_head_orphan( RepositorySafeHandle repo ) {
			IntPtr module;
			var d = (d_git_repository_head_orphan)getDelegate<d_git_repository_head_orphan>( out module, "_git_repository_head_orphan@4" );

			var r = d( repo );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_repository_index( out IndexSafeHandle index, RepositorySafeHandle repo );

		internal static int git_repository_index( out IndexSafeHandle index, RepositorySafeHandle repo ) {
			IntPtr module;
			var d = (d_git_repository_index)getDelegate<d_git_repository_index>( out module, "_git_repository_index@8" );

			var r = d( out index, repo );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_repository_init(
			out RepositorySafeHandle repository,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath path,
			[MarshalAs( UnmanagedType.Bool )] bool isBare );

		internal static int git_repository_init(
			out RepositorySafeHandle repository,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath path,
			[MarshalAs( UnmanagedType.Bool )] bool isBare ) {
			IntPtr module;
			var d = (d_git_repository_init)getDelegate<d_git_repository_init>( out module, "_git_repository_init@12" );

			var r = d( out repository, path, isBare );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_repository_is_bare( RepositorySafeHandle handle );

		internal static int git_repository_is_bare( RepositorySafeHandle handle ) {
			IntPtr module;
			var d = (d_git_repository_is_bare)getDelegate<d_git_repository_is_bare>( out module, "_git_repository_is_bare@4" );

			var result = d( handle );
			
			FreeLibrary( module );
			return result;
		}

		private delegate int d_git_repository_is_empty( RepositorySafeHandle repo );

		internal static int git_repository_is_empty( RepositorySafeHandle repo ) {
			IntPtr module;
			var d = (d_git_repository_is_empty)getDelegate<d_git_repository_is_empty>( out module, "_git_repository_is_empty@4" );

			var result = d( repo );
			
			FreeLibrary( module );
			return result;
		}

		private delegate int d_git_repository_merge_cleanup( RepositorySafeHandle repo );

		internal static int git_repository_merge_cleanup( RepositorySafeHandle repo ) {
			IntPtr module;
			var d = (d_git_repository_merge_cleanup)getDelegate<d_git_repository_merge_cleanup>( out module, "_git_repository_merge_cleanup@4" );

			var result = d( repo );

			FreeLibrary( module );
			return result;
		}

		internal delegate int git_repository_mergehead_foreach_cb(
			ref GitOid oid,
			IntPtr payload );

		private delegate int d_git_repository_mergehead_foreach(
			RepositorySafeHandle repo,
			git_repository_mergehead_foreach_cb cb,
			IntPtr payload );

		internal static int git_repository_mergehead_foreach( RepositorySafeHandle repo, git_repository_mergehead_foreach_cb cb, IntPtr payload ) {
			IntPtr module;
			var d = (d_git_repository_mergehead_foreach)getDelegate<d_git_repository_mergehead_foreach>( out module, "_git_repository_mergehead_foreach@12" );

			var result = d( repo, cb, payload );

			FreeLibrary( module );
			return result;
		}

		private delegate int d_git_repository_odb( out ObjectDatabaseSafeHandle odb, RepositorySafeHandle repo );

		internal static int git_repository_odb( out ObjectDatabaseSafeHandle odb, RepositorySafeHandle repo ) {
			IntPtr module;
			var d = (d_git_repository_odb)getDelegate<d_git_repository_odb>( out module, "_git_repository_odb@8" );

			var result = d( out odb, repo );

			FreeLibrary( module );
			return result;
		}

		private delegate int d_git_repository_open(
			out RepositorySafeHandle repository,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath path);

		internal static int git_repository_open( out RepositorySafeHandle repository, string path ) {
			IntPtr module;
			var d = (d_git_repository_open)getDelegate<d_git_repository_open>( out module, "_git_repository_open@8" );

			var result = d( out repository, path );

			FreeLibrary( module );
			return result;
		}

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathNoCleanupMarshaler ) )]
		private delegate FilePath d_git_repository_path( RepositorySafeHandle repository );

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathNoCleanupMarshaler ) )]
		internal static FilePath git_repository_path( RepositorySafeHandle repository ) {
			IntPtr module;
			var d = (d_git_repository_path)getDelegate<d_git_repository_path>( out module, "_git_repository_path@4" );

			var result = d( repository );

			FreeLibrary( module );
			return result;
		}

		private delegate void d_git_repository_set_config(
			RepositorySafeHandle repository,
			ConfigurationSafeHandle config );

		internal static void git_repository_set_config( RepositorySafeHandle repository, ConfigurationSafeHandle config ) {
			IntPtr module;
			var d = (d_git_repository_set_config)getDelegate<d_git_repository_set_config>( out module, "_git_repository_set_config@8" );

			d( repository, config );

			FreeLibrary( module );
		}

		private delegate void d_git_repository_set_index(
			RepositorySafeHandle repository,
			IndexSafeHandle index );

		internal static void git_repository_set_index( RepositorySafeHandle repository, IndexSafeHandle index ) {
			IntPtr module;
			var d = (d_git_repository_set_index)getDelegate<d_git_repository_set_index>( out module, "_git_repository_set_index@8" );

			d( repository, index );

			FreeLibrary( module );
		}

		private delegate int d_git_repository_set_workdir(
			RepositorySafeHandle repository,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath workdir,
			bool update_gitlink );

		internal static int git_repository_set_workdir( RepositorySafeHandle repository, FilePath workdir, bool update_gitlink ) {
			IntPtr module;
			var d = (d_git_repository_set_workdir)getDelegate<d_git_repository_set_workdir>( out module, "_git_repository_set_workdir@12" );

			int result = d( repository, workdir, update_gitlink );

			FreeLibrary( module );
			return result;
		}

		private delegate int d_git_repository_state(
			RepositorySafeHandle repository );

		internal static int git_repository_state( RepositorySafeHandle repository ) {
			IntPtr module;
			var d = (d_git_repository_state)getDelegate<d_git_repository_state>( out module, "_git_repository_state@4" );

			var result = d( repository );

			FreeLibrary( module );
			return result;
		}

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathNoCleanupMarshaler ) )]
		private delegate FilePath d_git_repository_workdir( RepositorySafeHandle repository );

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathNoCleanupMarshaler ) )]
		internal static FilePath git_repository_workdir( RepositorySafeHandle repository ) {
			IntPtr module;
			var d = (d_git_repository_workdir)getDelegate<d_git_repository_workdir>( out module, "_git_repository_workdir@4" );

			var result = d( repository );

			FreeLibrary( module );
			return result;
		}

		private delegate int d_git_reset(
			RepositorySafeHandle repo,
			GitObjectSafeHandle target,
			ResetOptions reset_type );

		internal static int git_reset( RepositorySafeHandle repo, GitObjectSafeHandle target, ResetOptions reset_type ) {
			IntPtr module;
			var d = (d_git_reset)getDelegate<d_git_reset>( out module, "_git_reset@12" );

			var result = d( repo, target, reset_type );

			FreeLibrary( module );
			return result;
		}

		private delegate int d_git_revparse_single(
			out GitObjectSafeHandle obj,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string spec );

		internal static int git_revparse_single( out GitObjectSafeHandle obj, RepositorySafeHandle repo, string spec ) {
			IntPtr module;
			var d = (d_git_revparse_single)getDelegate<d_git_revparse_single>( out module, "_git_revparse_single@12" );

			var result = d( out obj, repo, spec );

			FreeLibrary( module );
			return result;
		}

		private delegate void d_git_revwalk_free( IntPtr walker );

		internal static void git_revwalk_free( IntPtr walker ) {
			IntPtr module;
			var d = (d_git_revwalk_free)getDelegate<d_git_revwalk_free>( out module, "_git_revwalk_free@4" );

			d( walker );

			FreeLibrary( module );
		}

		private delegate int d_git_revwalk_hide( RevWalkerSafeHandle walker, ref GitOid commit_id );

		internal static int git_revwalk_hide( RevWalkerSafeHandle walker, ref GitOid commit_id ) {
			IntPtr module;
			var d = (d_git_revwalk_hide)getDelegate<d_git_revwalk_hide>( out module, "_git_revwalk_hide@8" );

			var result = d( walker, ref commit_id );

			FreeLibrary( module );
			return result;
		}

		private delegate int d_git_revwalk_new( out RevWalkerSafeHandle walker, RepositorySafeHandle repo );

		internal static int git_revwalk_new( out RevWalkerSafeHandle walker, RepositorySafeHandle repo ) {
			IntPtr module;
			var d = (d_git_revwalk_new)getDelegate<d_git_revwalk_new>( out module, "_git_revwalk_new@8" );

			var result = d( out walker, repo );

			FreeLibrary( module );
			return result;
		}

		private delegate int d_git_revwalk_next( out GitOid id, RevWalkerSafeHandle walker );

		internal static int git_revwalk_next( out GitOid id, RevWalkerSafeHandle walker ) {
			IntPtr module;
			var d = (d_git_revwalk_next)getDelegate<d_git_revwalk_next>( out module, "_git_revwalk_next@8" );

			var result = d( out id, walker );

			FreeLibrary( module );
			return result;
		}

		private delegate int d_git_revwalk_push( RevWalkerSafeHandle walker, ref GitOid id );

		internal static int git_revwalk_push( RevWalkerSafeHandle walker, ref GitOid id ) {
			IntPtr module;
			var d = (d_git_revwalk_push)getDelegate<d_git_revwalk_push>( out module, "_git_revwalk_push@8" );

			var result = d( walker, ref id );

			FreeLibrary( module );
			return result;
		}

		private delegate void d_git_revwalk_reset( RevWalkerSafeHandle walker );

		internal static void git_revwalk_reset( RevWalkerSafeHandle walker ) {
			IntPtr module;
			var d = (d_git_revwalk_reset)getDelegate<d_git_revwalk_reset>( out module, "_git_revwalk_reset@4" );

			d( walker );

			FreeLibrary( module );
		}

		private delegate void d_git_revwalk_sorting( RevWalkerSafeHandle walk, GitSortOptions sort );

		internal static void git_revwalk_sorting( RevWalkerSafeHandle walk, GitSortOptions sort ) {
			IntPtr module;
			var d = (d_git_revwalk_sorting)getDelegate<d_git_revwalk_sorting>( out module, "_git_revwalk_sorting@8" );

			d( walk, sort );

			FreeLibrary( module );
		}

		private delegate void d_git_signature_free( IntPtr signature );

		internal static void git_signature_free( IntPtr signature ) {
			IntPtr module;
			var d = (d_git_signature_free)getDelegate<d_git_signature_free>( out module, "_git_signature_free@4" );

			d( signature );

			FreeLibrary( module );
		}

		private delegate int d_git_signature_new(
			out SignatureSafeHandle signature,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string name,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string email,
			long time,
			int offset );

		internal static int git_signature_new( out SignatureSafeHandle signature, string name, string email, long time, int offset ) {
			IntPtr module;
			var d = (d_git_signature_new)getDelegate<d_git_signature_new>( out module, "_git_signature_new@24" );

			var r = d( out signature, name, email, time, offset );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_status_file(
			out FileStatus statusflags,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath filepath );

		internal static int git_status_file( out FileStatus statusflags, RepositorySafeHandle repo, FilePath filepath ) {
			IntPtr module;
			var d = (d_git_status_file)getDelegate<d_git_status_file>( out module, "_git_status_file@12" );

			var r = d( out statusflags, repo, filepath );

			FreeLibrary( module );
			return r;
		}

		internal delegate int git_status_cb(
			IntPtr path,
			uint statusflags,
			IntPtr payload );

		private delegate int d_git_status_foreach( RepositorySafeHandle repo, git_status_cb cb, IntPtr payload );

		internal static int git_status_foreach( RepositorySafeHandle repo, git_status_cb cb, IntPtr payload ) {
			IntPtr module;
			var d = (d_git_status_foreach)getDelegate<d_git_status_foreach>( out module, "_git_status_foreach@12" );

			var r = d( repo, cb, payload );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_tag_create(
			out GitOid oid,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string name,
			GitObjectSafeHandle target,
			SignatureSafeHandle signature,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string message,
			[MarshalAs( UnmanagedType.Bool )] bool force );

		internal static int git_tag_create( out GitOid oid, RepositorySafeHandle repo, string name, GitObjectSafeHandle target, SignatureSafeHandle signature, string message, bool force ) {
			IntPtr module;
			var d = (d_git_tag_create)getDelegate<d_git_tag_create>( out module, "_git_tag_create@28" );

			var r = d( out oid, repo, name, target, signature, message, force );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_tag_create_lightweight(
			out GitOid oid,
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string name,
			GitObjectSafeHandle target,
			[MarshalAs( UnmanagedType.Bool )] bool force );

		internal static int git_tag_create_lightweight( out GitOid oid, RepositorySafeHandle repo, string name, GitObjectSafeHandle target, bool force ) {
			IntPtr module;
			var d = (d_git_tag_create_lightweight)getDelegate<d_git_tag_create_lightweight>( out module, "_git_tag_create_lightweight@20" );

			var r = d( out oid, repo, name, target, force );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_tag_delete(
			RepositorySafeHandle repo,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string tagName );

		internal static int git_tag_delete( RepositorySafeHandle repo, string tagName ) {
			IntPtr module;
			var d = (d_git_tag_delete)getDelegate<d_git_tag_delete>( out module, "_git_tag_delete@8" );

			var r = d( repo, tagName );

			FreeLibrary( module );
			return r;
		}

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		private delegate string d_git_tag_message( GitObjectSafeHandle tag );

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		internal static string git_tag_message( GitObjectSafeHandle tag ) {
			IntPtr module;
			var d = (d_git_tag_message)getDelegate<d_git_tag_message>( out module, "_git_tag_message@4" );

			var r = d( tag );

			FreeLibrary( module );
			return r;
		}

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		private delegate string d_git_tag_name( GitObjectSafeHandle tag );

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		internal static string git_tag_name( GitObjectSafeHandle tag ) {
			IntPtr module;
			var d = (d_git_tag_name)getDelegate<d_git_tag_name>( out module, "_git_tag_name@4" );

			var r = d( tag );

			FreeLibrary( module );
			return r;
		}

		private delegate IntPtr d_git_tag_tagger( GitObjectSafeHandle tag );

		internal static IntPtr git_tag_tagger( GitObjectSafeHandle tag ) {
			IntPtr module;
			var d = (d_git_tag_tagger)getDelegate<d_git_tag_tagger>( out module, "_git_tag_tagger@4" );

			var r = d( tag );

			FreeLibrary( module );
			return r;
		}

		private delegate OidSafeHandle d_git_tag_target_id( GitObjectSafeHandle tag );

		internal static OidSafeHandle git_tag_target_id( GitObjectSafeHandle tag ) {
			IntPtr module;
			var d = (d_git_tag_target_id)getDelegate<d_git_tag_target_id>( out module, "_git_tag_target_id@4" );

			var r = d( tag );

			FreeLibrary( module );
			return r;
		}

		private delegate GitObjectType d_git_tag_target_type( GitObjectSafeHandle tag );

		internal static GitObjectType git_tag_target_type( GitObjectSafeHandle tag ) {
			IntPtr module;
			var d = (d_git_tag_target_type)getDelegate<d_git_tag_target_type>( out module, "_git_tag_target_type@4" );

			var r = d( tag );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_threads_init();

		internal static int git_threads_init() {
			IntPtr module;
			var d = (d_git_threads_init)getDelegate<d_git_threads_init>( out module, "_git_threads_init@0" );

			var r = d();

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_threads_shutdown();

		internal static void git_threads_shutdown() {
			IntPtr module;
			var d = (d_git_threads_shutdown)getDelegate<d_git_threads_shutdown>( out module, "_git_threads_shutdown@0" );

			d();

			FreeLibrary( module );
		}

		internal delegate void git_transfer_progress_callback( ref GitTransferProgress stats, IntPtr payload );

		private delegate uint d_git_tree_entry_filemode( SafeHandle entry );

		internal static uint git_tree_entry_filemode( SafeHandle entry ) {
			IntPtr module;
			var d = (d_git_tree_entry_filemode)getDelegate<d_git_tree_entry_filemode>( out module, "_git_tree_entry_filemode@4" );

			var r = d( entry );

			FreeLibrary( module );
			return r;
		}

		private delegate TreeEntrySafeHandle d_git_tree_entry_byindex( GitObjectSafeHandle tree, UIntPtr idx );

		internal static TreeEntrySafeHandle git_tree_entry_byindex( GitObjectSafeHandle tree, UIntPtr idx ) {
			IntPtr module;
			var d = (d_git_tree_entry_byindex)getDelegate<d_git_tree_entry_byindex>( out module, "_git_tree_entry_byindex@8" );

			var r = d( tree, idx );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_tree_entry_bypath(
			out TreeEntrySafeHandle_Owned tree,
			GitObjectSafeHandle root,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( FilePathMarshaler ) )] FilePath treeentry_path );

		internal static int git_tree_entry_bypath( out TreeEntrySafeHandle_Owned tree, GitObjectSafeHandle root, FilePath treeentry_path ) {
			IntPtr module;
			var d = (d_git_tree_entry_bypath)getDelegate<d_git_tree_entry_bypath>( out module, "_git_tree_entry_bypath@12" );

			var r = d( out tree, root, treeentry_path );

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_tree_entry_free( IntPtr treeEntry );

		internal static void git_tree_entry_free( IntPtr treeEntry ) {
			IntPtr module;
			var d = (d_git_tree_entry_free)getDelegate<d_git_tree_entry_free>( out module, "_git_tree_entry_free@4" );

			d( treeEntry );

			FreeLibrary( module );
		}

		private delegate OidSafeHandle d_git_tree_entry_id( SafeHandle entry );

		internal static OidSafeHandle git_tree_entry_id( SafeHandle entry ) {
			IntPtr module;
			var d = (d_git_tree_entry_id)getDelegate<d_git_tree_entry_id>( out module, "_git_tree_entry_id@4" );

			var r = d( entry );

			FreeLibrary( module );
			return r;
		}

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		private delegate string d_git_tree_entry_name( SafeHandle entry );

		[return: MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8NoCleanupMarshaler ) )]
		internal static string git_tree_entry_name( SafeHandle entry ) {
			IntPtr module;
			var d = (d_git_tree_entry_name)getDelegate<d_git_tree_entry_name>( out module, "_git_tree_entry_name@4" );

			var r = d( entry );

			FreeLibrary( module );
			return r;
		}

		private delegate GitObjectType d_git_tree_entry_type( SafeHandle entry );

		internal static GitObjectType git_tree_entry_type( SafeHandle entry ) {
			IntPtr module;
			var d = (d_git_tree_entry_type)getDelegate<d_git_tree_entry_type>( out module, "_git_tree_entry_type@4" );

			var r = d( entry );

			FreeLibrary( module );
			return r;
		}

		private delegate UIntPtr d_git_tree_entrycount( GitObjectSafeHandle tree );

		internal static UIntPtr git_tree_entrycount( GitObjectSafeHandle tree ) {
			IntPtr module;
			var d = (d_git_tree_entrycount)getDelegate<d_git_tree_entrycount>( out module, "_git_tree_entrycount@4" );

			var r = d( tree );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_treebuilder_create( out TreeBuilderSafeHandle builder, IntPtr src );

		internal static int git_treebuilder_create( out TreeBuilderSafeHandle builder, IntPtr src ) {
			IntPtr module;
			var d = (d_git_treebuilder_create)getDelegate<d_git_treebuilder_create>( out module, "_git_treebuilder_create@8" );

			var r = d( out builder, src );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_treebuilder_insert(
			IntPtr entry_out,
			TreeBuilderSafeHandle builder,
			[MarshalAs( UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof( Utf8Marshaler ) )] string treeentry_name,
			ref GitOid id,
			uint attributes );

		internal static int git_treebuilder_insert( IntPtr entry_out, TreeBuilderSafeHandle builder, string treeentry_name, ref GitOid id, uint attributes ) {
			IntPtr module;
			var d = (d_git_treebuilder_insert)getDelegate<d_git_treebuilder_insert>( out module, "_git_treebuilder_insert@20" );

			var r = d( entry_out, builder, treeentry_name, ref id, attributes );

			FreeLibrary( module );
			return r;
		}

		private delegate int d_git_treebuilder_write( out GitOid id, RepositorySafeHandle repo, TreeBuilderSafeHandle bld );

		internal static int git_treebuilder_write( out GitOid id, RepositorySafeHandle repo, TreeBuilderSafeHandle bld ) {
			IntPtr module;
			var d = (d_git_treebuilder_write)getDelegate<d_git_treebuilder_write>( out module, "_git_treebuilder_write@12" );

			var r = d( out id, repo, bld );

			FreeLibrary( module );
			return r;
		}

		private delegate void d_git_treebuilder_free( IntPtr bld );

		internal static void git_treebuilder_free( IntPtr bld ) {
			IntPtr module;
			var d = (d_git_treebuilder_free)getDelegate<d_git_treebuilder_free>( out module, "_git_treebuilder_free@4" );

			d( bld );

			FreeLibrary( module );
		}

		private delegate int d_git_blob_is_binary( GitObjectSafeHandle blob );

		internal static int git_blob_is_binary( GitObjectSafeHandle blob ) {
			IntPtr module;
			var d = (d_git_blob_is_binary)getDelegate<d_git_blob_is_binary>( out module, "_git_blob_is_binary@4" );

			var r = d( blob );

			FreeLibrary( module );
			return r;
		}
	}
}

// ReSharper restore InconsistentNaming

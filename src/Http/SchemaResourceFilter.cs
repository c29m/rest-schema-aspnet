using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RestSchema.Http
{
    internal sealed class SchemaResourceFilter : IResourceFilter
    {
        public void OnResourceExecuting( ResourceExecutingContext context )
        {
            if ( !EnsureSchemaVersionIsValid( context ) )
            {
                return;
            }

            if ( !context.HttpContext.Request.Headers.ContainsKey( SchemaHeaders.SchemaMapping ) &&
                 !context.HttpContext.Request.Headers.ContainsKey( SchemaHeaders.SchemaInclude ) )
            {
                // no schema headers... move on...
                return;
            }

            // validate schema data on X-Schema-Map and X-Schema-Include
            var headerName = SchemaHeaders.SchemaMapping;
            if ( !context.HttpContext.Request.Headers.ContainsKey( headerName ) )
            {
                // Schema-Mapping takes precedence, but if not found...
                headerName = SchemaHeaders.SchemaInclude;
            }

            var headerValue = context.HttpContext.Request.Headers[headerName];

            try
            {
                var schema = Decoders.SchemaDecoder.Decode( headerValue );

                // explicit schema version needs to be verified as well
                if ( !string.IsNullOrEmpty( schema.Version ) &&
                   ( Version.Parse( schema.Version ) > SchemaVersion.Value ) )
                {
                    throw new NotSupportedException( "Version not supported." );
                }
            }
            catch ( Exception ex )
            {
                ReplyWithBadRequest( context, headerName, "Invalid format! " + ex.Message );

                return;
            }
        }

        public void OnResourceExecuted( ResourceExecutedContext context )
        {
        }

        private bool EnsureSchemaVersionIsValid( ResourceExecutingContext context )
        {
            // look for X-Schema-Version and see if it matches the server spec
            if ( !context.HttpContext.Request.Headers.TryGetValue( SchemaHeaders.SchemaVersion, out var versionValue ) )
            {
                // header not present... move on...
                return ( true );
            }

            if ( !Version.TryParse( versionValue, out var version ) )
            {
                // invalid version format!
                ReplyWithBadRequest( context, SchemaHeaders.SchemaVersion, "Invalid format!" );

                return ( false );
            }

            if ( version > SchemaVersion.Value )
            {
                // schema data spec version is higher than the server's spec version
                ReplyWithBadRequest( context, SchemaHeaders.SchemaVersion, "Version not supported." );

                return ( false );
            }

            return ( true );
        }

        private void ReplyWithBadRequest( ResourceExecutingContext context, string header, string errorMessage )
        {
            context.ModelState.AddModelError( header, errorMessage );

            context.Result = new BadRequestObjectResult( context.ModelState );

            context.HttpContext.Response.Headers.Add( SchemaHeaders.SchemaVersion
                , SchemaVersion.Value.ToString() );
        }
    }
}

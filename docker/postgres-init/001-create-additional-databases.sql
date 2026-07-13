-- POSTGRES_DB creates the default "arcadelink_users" database; the other services with state
-- (Catalog) need their own isolated database in the same Postgres instance.
CREATE DATABASE arcadelink_catalog;

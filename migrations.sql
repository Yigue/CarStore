CREATE TABLE IF NOT EXISTS public."__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;
INSERT INTO public."__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20250127000000_AddValueObjects', '9.0.2');

CREATE TABLE public.clients (
    id uuid NOT NULL,
    first_name character varying(100) NOT NULL,
    last_name character varying(100) NOT NULL,
    dni character varying(20) NOT NULL,
    email character varying(200) NOT NULL,
    phone character varying(20) NOT NULL,
    address character varying(200) NOT NULL,
    status text NOT NULL,
    created_at timestamp with time zone NOT NULL,
    update_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_clients PRIMARY KEY (id)
);

CREATE TABLE public.marca (
    id uuid NOT NULL,
    nombre character varying(100) NOT NULL,
    CONSTRAINT pk_marca PRIMARY KEY (id)
);

CREATE TABLE public.transaction_categories (
    id uuid NOT NULL,
    name text NOT NULL,
    description text NOT NULL,
    type integer NOT NULL,
    is_active boolean NOT NULL,
    CONSTRAINT pk_transaction_categories PRIMARY KEY (id)
);

CREATE TABLE public.users (
    id uuid NOT NULL,
    email text NOT NULL,
    first_name text NOT NULL,
    last_name text NOT NULL,
    password_hash text NOT NULL,
    CONSTRAINT pk_users PRIMARY KEY (id)
);

CREATE TABLE public.modelo (
    id uuid NOT NULL,
    nombre character varying(100) NOT NULL,
    marca_id uuid NOT NULL,
    CONSTRAINT pk_modelo PRIMARY KEY (id),
    CONSTRAINT fk_modelo_marca_marca_id FOREIGN KEY (marca_id) REFERENCES public.marca (id) ON DELETE RESTRICT
);

CREATE TABLE public.cars (
    id uuid NOT NULL,
    marca_id uuid NOT NULL,
    modelo_id uuid NOT NULL,
    color integer NOT NULL,
    car_type text NOT NULL,
    car_status text NOT NULL,
    service_car text NOT NULL,
    fuel_type text NOT NULL,
    cantidad_puertas integer NOT NULL,
    cantidad_asientos integer NOT NULL,
    cilindrada integer NOT NULL,
    kilometraje integer NOT NULL,
    año integer NOT NULL,
    patente character varying(10) NOT NULL,
    descripcion character varying(500) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    price numeric NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_cars PRIMARY KEY (id),
    CONSTRAINT fk_cars_marca_marca_id FOREIGN KEY (marca_id) REFERENCES public.marca (id) ON DELETE CASCADE,
    CONSTRAINT fk_cars_modelo_modelo_id FOREIGN KEY (modelo_id) REFERENCES public.modelo (id) ON DELETE CASCADE
);

CREATE TABLE public.quotes (
    id uuid NOT NULL,
    car_id uuid NOT NULL,
    client_id uuid NOT NULL,
    proposed_price numeric(18,2) NOT NULL,
    status text NOT NULL,
    valid_until timestamp with time zone NOT NULL,
    comments character varying(500) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_quotes PRIMARY KEY (id),
    CONSTRAINT fk_quotes_cars_car_id FOREIGN KEY (car_id) REFERENCES public.cars (id) ON DELETE RESTRICT,
    CONSTRAINT fk_quotes_clients_client_id FOREIGN KEY (client_id) REFERENCES public.clients (id) ON DELETE RESTRICT
);

CREATE TABLE public.sales (
    id uuid NOT NULL,
    car_id uuid NOT NULL,
    client_id uuid NOT NULL,
    final_price numeric(18,2) NOT NULL,
    status text NOT NULL,
    payment_method text NOT NULL,
    contract_number character varying(50) NOT NULL,
    sale_date timestamp with time zone NOT NULL,
    comments character varying(500) NOT NULL,
    CONSTRAINT pk_sales PRIMARY KEY (id),
    CONSTRAINT fk_sales_cars_car_id FOREIGN KEY (car_id) REFERENCES public.cars (id) ON DELETE RESTRICT,
    CONSTRAINT fk_sales_clients_client_id FOREIGN KEY (client_id) REFERENCES public.clients (id) ON DELETE RESTRICT
);

CREATE TABLE public.transactions (
    id uuid NOT NULL,
    type integer NOT NULL,
    amount numeric(18,2) NOT NULL,
    description character varying(500) NOT NULL,
    payment_method integer NOT NULL,
    reference_number character varying(50),
    transaction_date timestamp with time zone NOT NULL,
    category_id uuid NOT NULL,
    car_id uuid,
    client_id uuid,
    sale_id uuid,
    CONSTRAINT pk_transactions PRIMARY KEY (id),
    CONSTRAINT fk_transactions_cars_car_id FOREIGN KEY (car_id) REFERENCES public.cars (id) ON DELETE RESTRICT,
    CONSTRAINT fk_transactions_clients_client_id FOREIGN KEY (client_id) REFERENCES public.clients (id) ON DELETE RESTRICT,
    CONSTRAINT fk_transactions_sales_sale_id FOREIGN KEY (sale_id) REFERENCES public.sales (id) ON DELETE RESTRICT,
    CONSTRAINT fk_transactions_transaction_categories_category_id FOREIGN KEY (category_id) REFERENCES public.transaction_categories (id) ON DELETE CASCADE
);

CREATE INDEX ix_cars_marca_id ON public.cars (marca_id);

CREATE INDEX ix_cars_modelo_id ON public.cars (modelo_id);

CREATE UNIQUE INDEX ix_cars_patente ON public.cars (patente);

CREATE UNIQUE INDEX ix_clients_dni ON public.clients (dni);

CREATE INDEX ix_modelo_marca_id ON public.modelo (marca_id);

CREATE INDEX ix_quotes_car_id ON public.quotes (car_id);

CREATE INDEX ix_quotes_client_id ON public.quotes (client_id);

CREATE INDEX ix_sales_car_id ON public.sales (car_id);

CREATE INDEX ix_sales_client_id ON public.sales (client_id);

CREATE INDEX ix_transactions_car_id ON public.transactions (car_id);

CREATE INDEX ix_transactions_category_id ON public.transactions (category_id);

CREATE INDEX ix_transactions_client_id ON public.transactions (client_id);

CREATE INDEX ix_transactions_sale_id ON public.transactions (sale_id);

CREATE UNIQUE INDEX ix_users_email ON public.users (email);

INSERT INTO public."__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20250205134503_InitialMigration', '9.0.2');

CREATE TABLE public.car_image (
    id uuid NOT NULL,
    car_id uuid NOT NULL,
    image_url text NOT NULL,
    is_primary boolean NOT NULL,
    "order" integer NOT NULL,
    CONSTRAINT pk_car_image PRIMARY KEY (id),
    CONSTRAINT fk_car_image_cars_car_id FOREIGN KEY (car_id) REFERENCES public.cars (id) ON DELETE CASCADE
);

CREATE INDEX ix_car_image_car_id ON public.car_image (car_id);

INSERT INTO public."__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20250205201226_UpdateCarImageIdToGuid', '9.0.2');

ALTER TABLE public.car_image DROP CONSTRAINT fk_car_image_cars_car_id;

ALTER TABLE public.car_image DROP CONSTRAINT pk_car_image;

ALTER TABLE public.car_image RENAME TO car_images;

ALTER INDEX public.ix_car_image_car_id RENAME TO ix_car_images_car_id;

ALTER TABLE public.car_images ALTER COLUMN "order" SET DEFAULT 0;

ALTER TABLE public.car_images ALTER COLUMN is_primary SET DEFAULT FALSE;

ALTER TABLE public.car_images ALTER COLUMN image_url TYPE character varying(500);

ALTER TABLE public.car_images ADD CONSTRAINT pk_car_images PRIMARY KEY (id);

ALTER TABLE public.car_images ADD CONSTRAINT fk_car_images_cars_car_id FOREIGN KEY (car_id) REFERENCES public.cars (id) ON DELETE CASCADE;

INSERT INTO public."__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20250402190501_AddCarImages', '9.0.2');

ALTER TABLE public.transaction_categories DROP COLUMN is_active;

ALTER TABLE public.transactions ALTER COLUMN amount TYPE numeric;

ALTER TABLE public.sales ALTER COLUMN final_price TYPE numeric;

ALTER TABLE public.quotes ALTER COLUMN proposed_price TYPE numeric;

CREATE TABLE public."UserPermissions" (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    permission character varying(100) NOT NULL,
    CONSTRAINT pk_user_permissions PRIMARY KEY (id),
    CONSTRAINT fk_user_permissions_users_user_id FOREIGN KEY (user_id) REFERENCES public.users (id) ON DELETE CASCADE
);

CREATE INDEX ix_user_permissions_user_id ON public."UserPermissions" (user_id);

INSERT INTO public."__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20260111204117_AddUserPermissions', '9.0.2');

CREATE TABLE public."OutboxMessages" (
    id uuid NOT NULL,
    type text NOT NULL,
    content text NOT NULL,
    occurred_on_utc timestamp with time zone NOT NULL,
    processed_on_utc timestamp with time zone,
    error text,
    CONSTRAINT pk_outbox_messages PRIMARY KEY (id)
);

INSERT INTO public."__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20260111212656_AddOutboxPattern', '9.0.2');

ALTER TABLE public.users ADD dealer_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE public."UserPermissions" ADD dealer_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE public.transactions ADD dealer_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE public.transaction_categories ADD dealer_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE public.sales ADD dealer_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE public.quotes ADD dealer_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE public.modelo ADD dealer_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE public.marca ADD dealer_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE public.clients ADD dealer_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE public.cars ADD dealer_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE public.car_images ADD dealer_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

INSERT INTO public."__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20260201024857_AddDealerIdToEntities', '9.0.2');

ALTER TABLE public.transaction_categories DROP COLUMN dealer_id;

ALTER TABLE public.modelo DROP COLUMN dealer_id;

ALTER TABLE public.marca DROP COLUMN dealer_id;

ALTER TABLE public.car_images DROP COLUMN dealer_id;

INSERT INTO public."__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20260323002031_FixModelMismatchV2', '9.0.2');

COMMIT;


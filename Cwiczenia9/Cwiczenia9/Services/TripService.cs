﻿using Cwiczenia9.Data;
using Cwiczenia9.Exceptions;
using Cwiczenia9.ExtensionMethods;
using Cwiczenia9.Models;
using Cwiczenia9.RequestModels;
using Cwiczenia9.ResponseModels;
using Microsoft.EntityFrameworkCore;

namespace Cwiczenia9.Services;

public class TripService(MasterContext context) : ITripService
{
    public async Task<PagedResult<GetTripsResponseModel>> GetTrips(int page, int pageSize, CancellationToken cancellationToken)
    {
        var totalTrips = await context.Trips.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalTrips / (double)pageSize);

        var trips = await context.Trips
            .Select(trip => new GetTripsResponseModel
            {
                Name = trip.Name,
                Description = trip.Description,
                DateFrom = trip.DateFrom,
                DateTo = trip.DateTo,
                MaxPeople = trip.MaxPeople,
                Countries = trip.IdCountries.Select(country => new CountryDetails
                {
                    Name = country.Name
                }).ToList(),
                Clients = trip.ClientTrips.Select(clientTrip => new ClientDetails
                {
                    FirstName = clientTrip.IdClientNavigation.FirstName,
                    LastName = clientTrip.IdClientNavigation.LastName
                }).ToList()
            })
            .OrderByDescending(e => e.DateFrom)
            .Paginate(page, pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<GetTripsResponseModel>
        {
            PageNum = page,
            PageSize = pageSize,
            AllPages = totalPages,
            Trips = trips
        };
    }

    public async Task AssignAClientToTheTripAsync(int idTrip, AssignAClientToTheTripRequestModel requestModel,
        CancellationToken cancellationToken)
    {
        var client = await context.Clients.SingleOrDefaultAsync(e => e.Pesel == requestModel.Pesel, cancellationToken);


        if (client is null)
        {
            client = new Client
            {
                FirstName = requestModel.FirstName,
                LastName = requestModel.LastName,
                Email = requestModel.Email,
                Telephone = requestModel.Telephone,
                Pesel = requestModel.Pesel
            };
            await context.AddAsync(client, cancellationToken);
        }

        else
        {
            var tripAssignment =
                await context.ClientTrips.SingleOrDefaultAsync(e =>
                    e.IdClient == client.IdClient && e.IdTrip == idTrip, cancellationToken);

            if (tripAssignment is not null)
            {
                throw new BadRequestException("Client is assigned to this trip already");
            }
        }


        var trip = await context.Trips.SingleOrDefaultAsync(e => e.IdTrip == idTrip, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException($"Trip with id:{idTrip} does not exist");
        }


        var clientTrip = new ClientTrip
        {

            IdClientNavigation = client,

            IdTripNavigation = trip,
            PaymentDate = requestModel.PaymentDate,
            RegisteredAt = DateTime.Now
        };
        await context.ClientTrips.AddAsync(clientTrip, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    }
